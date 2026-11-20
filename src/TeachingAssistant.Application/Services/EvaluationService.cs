using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Compares every approved report with the AI draft it was made from and adds the comparisons up: how close the scores are,
/// which of the AI's mistakes and strengths the teacher kept, edited or rejected, and the same per model. A report whose draft
/// was later discarded (for example because another draft of the submission was approved) no longer counts; discarded
/// drafts are counted and listed apart, with why. Everything is counted in code from stored data; no model is called.
/// </summary>
public class EvaluationService : IEvaluationService
{
    /// <summary>How close counts as agreeing: 5% of the maximum, which is 5 points out of 100.</summary>
    public const double TolerancePercent = 5;

    private const int BucketSize = 5;
    private const int BucketLimit = 25;

    private readonly IGradingReportStore _reports;
    private readonly IGradingStore _gradings;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentStore _students;
    private readonly ICourseAssignmentStore _assignments;
    private readonly IEvaluationRunStore _runs;

    public EvaluationService(
        IGradingReportStore reports, IGradingStore gradings, ISubmissionStore submissions, IStudentStore students,
        ICourseAssignmentStore assignments, IEvaluationRunStore runs)
    {
        _reports = reports;
        _gradings = gradings;
        _submissions = submissions;
        _students = students;
        _assignments = assignments;
        _runs = runs;
    }

    public async Task<EvaluationResponse> GetAsync(string? courseId = null, string? model = null, CancellationToken cancellationToken = default)
    {
        if (model is not null)
        {
            return await GetForModelAsync(courseId, model, cancellationToken);
        }

        var approved = await _reports.GetApprovedAsync(courseId, cancellationToken);
        var compared = approved
            .Where(a => a.Draft.Status != GradingStatus.Discarded)
            .Select(a => (a.Report, a.Draft, Comparison: DraftComparison.Of(a.Draft, a.Report)))
            .ToList();
        var discarded = await _gradings.GetDiscardedAsync(courseId, cancellationToken);

        var (titles, grades, names) = await LookupsAsync(
            compared.Select(c => c.Report.CourseId).Concat(discarded.Select(d => d.CourseId)),
            compared.Select(c => c.Report.StudentId), cancellationToken);

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
            Classroom(compared.Select(c => (c.Comparison.AiTotal, c.Comparison.TeacherTotal, c.Comparison.MaxScore,
                grades.GetValueOrDefault((c.Report.AssignmentId, c.Report.StudentId)))).ToList()),
            compared.Select(c => c.Draft.Model)
                .Concat(discarded.Select(d => d.Model))
                .Distinct()
                .Select(model => Model(model,
                    compared.Where(c => c.Draft.Model == model).Select(c => c.Comparison).ToList(),
                    discarded.Count(d => d.Model == model && d.Reason == DiscardReason.PoorGrading)))
                .OrderByDescending(m => m.Reports)
                .ToList(),
            Discards(discarded, titles),
            rows);
    }

    /// <summary>
    /// One model's side gradings (its newest finished one per approved report, from evaluation runs) against the approved
    /// reports, in the same shape as the drafts' evaluation. Reports it has not graded yet are left out.
    /// </summary>
    private async Task<EvaluationResponse> GetForModelAsync(string? courseId, string model, CancellationToken cancellationToken)
    {
        GradingModels.EnsureKnown([model]);
        var approved = (await _reports.GetApprovedAsync(courseId, cancellationToken))
            .Where(a => a.Draft.Status != GradingStatus.Discarded)
            .ToDictionary(a => a.Report.Id);
        var compared = (await _runs.GetDoneAsync(cancellationToken))
            .Where(r => r.Model == model && approved.ContainsKey(r.ReportId))
            .GroupBy(r => r.ReportId)
            .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
            .Select(r =>
            {
                var pair = approved[r.ReportId];
                return (pair.Report, pair.Draft, Result: r, Comparison: RunComparison.Of(r, pair.Report, pair.Draft.MaxScore));
            })
            .ToList();

        var (titles, grades, names) = await LookupsAsync(
            compared.Select(c => c.Report.CourseId), compared.Select(c => c.Report.StudentId), cancellationToken);

        var rows = compared
            .Select(c => new EvaluationRow(
                c.Report.Id,
                c.Draft.Id,
                c.Report.CourseId,
                c.Report.AssignmentId,
                titles.GetValueOrDefault((c.Report.CourseId, c.Report.AssignmentId)),
                c.Report.StudentId,
                names.GetValueOrDefault(c.Report.StudentId),
                c.Result.Model,
                c.Comparison.ModelTotal,
                c.Comparison.TeacherTotal,
                c.Comparison.MaxScore,
                c.Comparison.ModelPercent,
                c.Comparison.TeacherPercent,
                c.Comparison.DifferencePercent,
                c.Result.Mistakes.Count,
                0,
                c.Comparison.RepeatedFalseAlarms,
                grades.GetValueOrDefault((c.Report.AssignmentId, c.Report.StudentId)),
                c.Report.ApprovedAt))
            .OrderByDescending(r => r.Difference is { } d ? Math.Abs(d) : -1)
            .ThenByDescending(r => r.ApprovedAt)
            .ToList();

        var comparisons = compared.Select(c => c.Comparison).ToList();
        return new EvaluationResponse(
            compared.Count,
            ScoresOf(comparisons.Select(c => c.DifferencePercent).OfType<double>().ToList()),
            MistakesOf(comparisons.SelectMany(c => c.Decisions).ToList()),
            new EvaluationStrengths(0, 0, 0, 0),
            Classroom(compared.Select(c => (c.Comparison.ModelTotal, c.Comparison.TeacherTotal, c.Comparison.MaxScore,
                grades.GetValueOrDefault((c.Report.AssignmentId, c.Report.StudentId)))).ToList()),
            [],
            new EvaluationDiscards(0, 0, [], []),
            rows,
            model,
            Found(approved.Count, comparisons));
    }

    internal static EvaluationFound Found(int approvedReports, IReadOnlyList<RunComparison> comparisons)
    {
        var results = comparisons.SelectMany(c => c.TeacherResults).ToList();
        return new EvaluationFound(
            approvedReports,
            results.Count,
            results.Count(r => r.Found),
            comparisons.Sum(c => c.RepeatedFalseAlarms),
            comparisons.Sum(c => c.Unreviewed),
            results.GroupBy(r => r.Category)
                .Select(g => new CategoryFound(g.Key, g.Count(), g.Count(r => r.Found)))
                .OrderByDescending(c => c.TeacherMistakes)
                .ToList(),
            Enum.GetValues<MistakeSeverity>()
                .Select(s => new SeverityFound(s, results.Count(r => r.Severity == s), results.Count(r => r.Severity == s && r.Found)))
                .Where(s => s.TeacherMistakes > 0)
                .ToList());
    }

    /// <summary>Assignment titles, returned Classroom grades and student names for the courses and students on the page.</summary>
    private async Task<(Dictionary<(string Course, string Assignment), string> Titles, Dictionary<(string Assignment, string Student), double?> Grades,
        Dictionary<string, string> Names)> LookupsAsync(IEnumerable<string> courseIds, IEnumerable<string> studentIds, CancellationToken cancellationToken)
    {
        var titles = new Dictionary<(string Course, string Assignment), string>();
        var grades = new Dictionary<(string Assignment, string Student), double?>();
        foreach (var course in courseIds.Distinct())
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

        var names = (await _students.FindByExternalIdsAsync(SubmissionSource.GoogleClassroom, studentIds.Distinct().ToList(), cancellationToken))
            .ToDictionary(s => s.ExternalUserId, s => s.Name);
        return (titles, grades, names);
    }

    internal static EvaluationScores Scores(IReadOnlyList<DraftComparison> comparisons)
    {
        return ScoresOf(comparisons.Select(c => c.DifferencePercent).OfType<double>().ToList());
    }

    /// <summary>The score numbers from the differences (AI or model minus teacher, in percent of the maximum).</summary>
    internal static EvaluationScores ScoresOf(IReadOnlyList<double> differences)
    {
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

        double? meanGap = differences.Count == 0 ? null : differences.Average(Math.Abs);
        return new EvaluationScores(
            differences.Count,
            meanGap is { } gap ? Math.Max(0, 100 - gap) : null,
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
        return MistakesOf(comparisons.SelectMany(c => c.Mistakes).ToList());
    }

    internal static EvaluationMistakes MistakesOf(IReadOnlyList<MistakeComparison> mistakes)
    {
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

    private static EvaluationClassroom Classroom(
        IReadOnlyList<(double? AiTotal, double? TeacherTotal, double? MaxScore, double? Grade)> pairs)
    {
        var graded = pairs.Where(p => p.Grade is not null && p.MaxScore is > 0).ToList();
        var aiDifferences = graded
            .Where(p => p.AiTotal is not null)
            .Select(p => (p.AiTotal!.Value - p.Grade!.Value) / p.MaxScore!.Value * 100)
            .ToList();
        var teacherDifferences = graded
            .Where(p => p.TeacherTotal is not null)
            .Select(p => (p.TeacherTotal!.Value - p.Grade!.Value) / p.MaxScore!.Value * 100)
            .ToList();

        return new EvaluationClassroom(
            graded.Count,
            aiDifferences.Count(d => Math.Abs(d) <= TolerancePercent + 1e-9),
            aiDifferences.Count == 0 ? null : aiDifferences.Average(Math.Abs),
            teacherDifferences.Count(d => Math.Abs(d) <= TolerancePercent + 1e-9));
    }

    private static EvaluationModel Model(string model, IReadOnlyList<DraftComparison> comparisons, int discardedPoorGrading)
    {
        var scores = Scores(comparisons);
        var mistakes = comparisons.SelectMany(c => c.Mistakes).ToList();
        return new EvaluationModel(model, comparisons.Count, scores.Compared, scores.Accuracy, scores.WithinTolerance, scores.MeanAbsoluteDifference,
            mistakes.Count, mistakes.Count(m => m.Decision == MistakeDecision.Rejected), discardedPoorGrading);
    }

    internal static EvaluationDiscards Discards(
        IReadOnlyList<DiscardedDraft> discarded, IReadOnlyDictionary<(string Course, string Assignment), string> titles)
    {
        var byReason = discarded
            .GroupBy(d => d.Reason)
            .Select(g => new DiscardReasonCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Reason == DiscardReason.PoorGrading)
            .ThenByDescending(c => c.Count)
            .ToList();
        var rows = discarded
            .Select(d => new DiscardedRow(d.GradingId, d.CourseId, d.AssignmentId, titles.GetValueOrDefault((d.CourseId, d.AssignmentId)),
                d.StudentId, d.StudentName, d.Model, d.Score is { } score ? score + (d.Bonus ?? 0) : null, d.MaxScore,
                d.Major, d.Minor, d.Style, d.Reason, d.Note, d.CreatedAt, d.DiscardedAt, d.ReportStatus))
            .ToList();
        return new EvaluationDiscards(discarded.Count, discarded.Count(d => d.Reason == DiscardReason.PoorGrading), byReason, rows);
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
