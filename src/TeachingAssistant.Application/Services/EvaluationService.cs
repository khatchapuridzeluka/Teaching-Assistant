using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Compares every approved report with the AI draft it was made from and adds the comparisons up: how close the scores are,
/// which of the AI's mistakes and strengths the teacher kept, edited or rejected, and the same per model. Everything is
/// counted in code from stored data; no model is called.
/// </summary>
public class EvaluationService : IEvaluationService
{
    /// <summary>How close counts as agreeing: 5% of the maximum, which is 5 points out of 100.</summary>
    public const double TolerancePercent = 5;

    private const int BucketSize = 5;
    private const int BucketLimit = 25;

    private readonly IGradingReportStore _reports;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentStore _students;
    private readonly ICourseAssignmentStore _assignments;

    public EvaluationService(
        IGradingReportStore reports, ISubmissionStore submissions, IStudentStore students, ICourseAssignmentStore assignments)
    {
        _reports = reports;
        _submissions = submissions;
        _students = students;
        _assignments = assignments;
    }

    public async Task<EvaluationResponse> GetAsync(string? courseId = null, CancellationToken cancellationToken = default)
    {
        var approved = await _reports.GetApprovedAsync(courseId, cancellationToken);
        var compared = approved.Select(a => (a.Report, a.Draft, Comparison: DraftComparison.Of(a.Draft, a.Report))).ToList();

        var titles = new Dictionary<(string Course, string Assignment), string>();
        var grades = new Dictionary<(string Assignment, string Student), double?>();
        foreach (var course in compared.Select(c => c.Report.CourseId).Distinct())
        {
            foreach (var assignment in await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, course, cancellationToken))
            {
                titles[(course, assignment.ExternalAssignmentId)] = assignment.Title;
            }

            foreach (var submission in await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, course, cancellationToken))
            {
                grades[(submission.AssignmentId, submission.StudentId)] = submission.Grade;
            }
        }

        var studentIds = compared.Select(c => c.Report.StudentId).Distinct().ToList();
        var names = (await _students.FindByExternalIdsAsync(SubmissionSource.GoogleClassroom, studentIds, cancellationToken))
            .ToDictionary(s => s.ExternalUserId, s => s.Name);

        var rows = compared
            .Select(c => Row(c.Report, c.Draft, c.Comparison, titles, names, grades.GetValueOrDefault((c.Report.AssignmentId, c.Report.StudentId))))
            .OrderByDescending(r => r.Difference is { } d ? Math.Abs(d) : -1)
            .ThenByDescending(r => r.ApprovedAt)
            .ToList();

        var comparisons = compared.Select(c => c.Comparison).ToList();
        return new EvaluationResponse(
            compared.Count,
            Scores(comparisons),
            Mistakes(comparisons),
            Strengths(comparisons),
            Classroom(compared.Select(c => (c.Comparison, grades.GetValueOrDefault((c.Report.AssignmentId, c.Report.StudentId)))).ToList()),
            compared.GroupBy(c => c.Draft.Model)
                .Select(g => Model(g.Key, g.Select(c => c.Comparison).ToList()))
                .OrderByDescending(m => m.Reports)
                .ToList(),
            rows);
    }

    internal static EvaluationScores Scores(IReadOnlyList<DraftComparison> comparisons)
    {
        var differences = comparisons.Select(c => c.DifferencePercent).OfType<double>().ToList();
        // 5-point steps from -25 to +25; anything further out lands in the outermost step.
        var steps = BucketLimit / BucketSize;
        var counts = new int[2 * steps];
        foreach (var difference in differences)
        {
            var step = Math.Clamp((int)Math.Floor(difference / BucketSize), -steps, steps - 1);
            counts[step + steps]++;
        }

        var buckets = counts
            .Select((count, i) => new DifferenceBucket(-BucketLimit + i * BucketSize, -BucketLimit + (i + 1) * BucketSize, count))
            .ToList();

        return new EvaluationScores(
            differences.Count,
            TolerancePercent,
            differences.Count(d => Math.Abs(d) <= TolerancePercent + 1e-9),
            differences.Count(d => Math.Abs(d) < 1e-9),
            differences.Count == 0 ? null : differences.Average(Math.Abs),
            differences.Count == 0 ? null : differences.Average(),
            differences.Count(d => d > 1e-9),
            differences.Count(d => d < -1e-9),
            buckets);
    }

    internal static EvaluationMistakes Mistakes(IReadOnlyList<DraftComparison> comparisons)
    {
        var mistakes = comparisons.SelectMany(c => c.Mistakes).ToList();
        var changed = mistakes.Where(m => m.PointsChanged).ToList();

        return new EvaluationMistakes(
            mistakes.Count,
            mistakes.Count(m => m.Decision == MistakeDecision.Kept),
            mistakes.Count(m => m.Decision == MistakeDecision.Edited),
            mistakes.Count(m => m.Decision == MistakeDecision.Rejected),
            changed.Count,
            changed.Count == 0 ? null : changed.Average(m => Math.Abs((m.TeacherPoints ?? 0) - (m.AiPoints ?? 0))),
            mistakes.GroupBy(m => m.Category)
                .Select(g => new CategoryDecisions(g.Key, Count(g, MistakeDecision.Kept), Count(g, MistakeDecision.Edited),
                    Count(g, MistakeDecision.Rejected)))
                .OrderByDescending(c => c.Kept + c.Edited + c.Rejected)
                .ToList(),
            Enum.GetValues<MistakeSeverity>()
                .Select(s => mistakes.Where(m => m.Severity == s).ToList())
                .Where(g => g.Count > 0)
                .Select(g => new SeverityDecisions(g[0].Severity, Count(g, MistakeDecision.Kept), Count(g, MistakeDecision.Edited),
                    Count(g, MistakeDecision.Rejected)))
                .ToList());
    }

    private static EvaluationStrengths Strengths(IReadOnlyList<DraftComparison> comparisons)
    {
        var kept = comparisons.Sum(c => c.StrengthsKept);
        var edited = comparisons.Sum(c => c.StrengthsEdited);
        var rejected = comparisons.Sum(c => c.StrengthsRejected);
        return new EvaluationStrengths(kept + edited + rejected, kept, edited, rejected);
    }

    private static EvaluationClassroom Classroom(IReadOnlyList<(DraftComparison Comparison, double? Grade)> pairs)
    {
        var graded = pairs.Where(p => p.Grade is not null && p.Comparison.MaxScore is > 0).ToList();
        var aiDifferences = graded
            .Where(p => p.Comparison.AiTotal is not null)
            .Select(p => (p.Comparison.AiTotal!.Value - p.Grade!.Value) / p.Comparison.MaxScore!.Value * 100)
            .ToList();
        var teacherDifferences = graded
            .Where(p => p.Comparison.TeacherTotal is not null)
            .Select(p => (p.Comparison.TeacherTotal!.Value - p.Grade!.Value) / p.Comparison.MaxScore!.Value * 100)
            .ToList();

        return new EvaluationClassroom(
            graded.Count,
            aiDifferences.Count(d => Math.Abs(d) <= TolerancePercent + 1e-9),
            aiDifferences.Count == 0 ? null : aiDifferences.Average(Math.Abs),
            teacherDifferences.Count(d => Math.Abs(d) <= TolerancePercent + 1e-9));
    }

    private static EvaluationModel Model(string model, IReadOnlyList<DraftComparison> comparisons)
    {
        var scores = Scores(comparisons);
        var mistakes = comparisons.SelectMany(c => c.Mistakes).ToList();
        return new EvaluationModel(model, comparisons.Count, scores.Compared, scores.WithinTolerance, scores.MeanAbsoluteDifference,
            mistakes.Count, mistakes.Count(m => m.Decision == MistakeDecision.Rejected));
    }

    private static EvaluationRow Row(
        GradingReport report, AiGrading draft, DraftComparison comparison, Dictionary<(string, string), string> titles,
        Dictionary<string, string> names, double? classroomGrade)
    {
        return new EvaluationRow(
            report.Id,
            draft.Id,
            report.CourseId,
            report.AssignmentId,
            titles.GetValueOrDefault((report.CourseId, report.AssignmentId)),
            report.StudentId,
            names.GetValueOrDefault(report.StudentId),
            draft.Model,
            comparison.AiTotal,
            comparison.TeacherTotal,
            comparison.MaxScore,
            comparison.AiPercent,
            comparison.TeacherPercent,
            comparison.DifferencePercent,
            comparison.Mistakes.Count,
            comparison.Mistakes.Count(m => m.Decision == MistakeDecision.Edited),
            comparison.Mistakes.Count(m => m.Decision == MistakeDecision.Rejected),
            classroomGrade,
            report.ApprovedAt);
    }

    private static int Count(IEnumerable<MistakeComparison> mistakes, MistakeDecision decision)
    {
        return mistakes.Count(m => m.Decision == decision);
    }
}
