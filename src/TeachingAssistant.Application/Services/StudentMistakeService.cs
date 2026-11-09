using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentMistakeService : IStudentMistakeService
{
    private readonly IStudentMistakeStore _mistakes;
    private readonly ICourseAssignmentStore _assignments;
    private readonly ISubmissionStore _submissions;
    private readonly IGradingReportStore _reports;

    public StudentMistakeService(
        IStudentMistakeStore mistakes, ICourseAssignmentStore assignments, ISubmissionStore submissions, IGradingReportStore reports)
    {
        _mistakes = mistakes;
        _assignments = assignments;
        _submissions = submissions;
        _reports = reports;
    }

    public async Task<StudentMistakesResponse> GetAsync(
        string courseId, string studentId, StudentMistakeFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new StudentMistakeFilter();
        var mistakes = await _mistakes.GetByStudentAsync(courseId, studentId, cancellationToken);
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);
        var classroom = (await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .Where(s => s.StudentId == studentId)
            .ToDictionary(s => s.AssignmentId);
        var latestReports = mistakes
            .GroupBy(m => m.AssignmentId)
            .ToDictionary(g => g.Key, g => g.MaxBy(m => m.ApprovedAt)!.ReportId);
        var reportScores = (await _reports.GetScoresAsync(latestReports.Values.Distinct().ToList(), cancellationToken))
            .ToDictionary(r => r.ReportId);

        DateTimeOffset? DueAt(string assignmentId)
        {
            return assignments.GetValueOrDefault(assignmentId)?.DueAt;
        }

        string? Title(string? assignmentId)
        {
            return assignmentId is null ? null : assignments.GetValueOrDefault(assignmentId)?.Title;
        }

        var labeled = MistakeHistory.Label(mistakes, DueAt);
        var options = labeled
            .GroupBy(l => l.Mistake.AssignmentId)
            .Select(g => new MistakeAssignmentOption(g.Key, Title(g.Key), DueAt(g.Key), g.Count()))
            .ToList();

        var selected = labeled
            .Where(l => filter.AssignmentId is null || l.Mistake.AssignmentId == filter.AssignmentId)
            .Where(l => filter.Category is null || l.Mistake.Category == filter.Category)
            .Where(l => filter.Severity is null || l.Mistake.Severity == filter.Severity)
            .Where(l => filter.Label is null || l.Label == filter.Label)
            .ToList();
        var total = selected.Count;

        var history = selected
            .AsEnumerable()
            .Reverse()
            .Select(l => new StudentMistakeResponse(l.Mistake.Id, l.Mistake.AssignmentId, Title(l.Mistake.AssignmentId),
                DueAt(l.Mistake.AssignmentId), l.Mistake.VersionId, l.Mistake.ReportId, l.Mistake.File, l.Mistake.StartLine,
                l.Mistake.EndLine, l.Mistake.Severity, l.Mistake.Category, l.Mistake.Message, l.Mistake.Points, l.Mistake.Decision,
                l.Label, Title(l.FirstAssignmentId), l.Mistake.ApprovedAt))
            .ToList();

        var categories = MistakeHistory.Trends(selected.Select(l => l.Mistake), DueAt)
            .Select(t => new MistakeCategoryResponse(t.Category, t.Count, Percent(t.Count, total), t.Assignments, t.Repeated,
                t.PointsLost, Title(t.FirstAssignmentId), Title(t.LastAssignmentId)))
            .ToList();

        var severities = Enum.GetValues<MistakeSeverity>()
            .Select(s => Share(s, selected.Count(l => l.Mistake.Severity == s), total))
            .ToList();

        var labels = Enum.GetValues<MistakeLabel>()
            .Select(label => Share(label, selected.Count(l => l.Label == label), total))
            .ToList();

        var byAssignment = selected
            .GroupBy(l => l.Mistake.AssignmentId)
            .Select(g =>
            {
                var submission = classroom.GetValueOrDefault(g.Key);
                var score = latestReports.TryGetValue(g.Key, out var reportId) ? reportScores.GetValueOrDefault(reportId) : null;
                var maxPoints = assignments.GetValueOrDefault(g.Key)?.MaxPoints ?? score?.MaxScore;
                return new MistakeAssignmentResponse(g.Key, Title(g.Key), DueAt(g.Key), g.Count(),
                    g.Count(l => l.Mistake.Severity == MistakeSeverity.Major),
                    g.Count(l => l.Mistake.Severity == MistakeSeverity.Minor),
                    g.Count(l => l.Mistake.Severity == MistakeSeverity.Style),
                    g.Count(l => l.Label == MistakeLabel.Repeated),
                    g.Sum(l => l.Mistake.Points ?? 0),
                    maxPoints,
                    submission?.InitialGrade,
                    submission?.FinalGrade,
                    score?.Score,
                    score?.Bonus,
                    score?.AiScore,
                    score?.AiBonus);
            })
            .ToList();

        var classroomGraded = byAssignment.Where(a => a is { ClassroomFinal: not null, MaxPoints: > 0 }).ToList();
        double? classroomLost = classroomGraded.Count == 0
            ? null
            : classroomGraded.Sum(a => Math.Max(0, a.MaxPoints!.Value - a.ClassroomFinal!.Value));

        return new StudentMistakesResponse(total, mistakes.Count, selected.Sum(l => l.Mistake.Points ?? 0), classroomLost,
            classroomGraded.Count, history, categories, severities, labels, byAssignment, options);
    }

    private static MistakeShare<T> Share<T>(T key, int count, int total)
    {
        return new MistakeShare<T>(key, count, Percent(count, total));
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
    }
}
