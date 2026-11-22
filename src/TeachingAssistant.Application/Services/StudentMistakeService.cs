using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentMistakeService : IStudentMistakeService
{
    private readonly StudentRecordCollector _records;
    private readonly ICourseAssignmentStore _assignments;
    private readonly ISubmissionStore _submissions;

    public StudentMistakeService(StudentRecordCollector records, ICourseAssignmentStore assignments, ISubmissionStore submissions)
    {
        _records = records;
        _assignments = assignments;
        _submissions = submissions;
    }

    public async Task<StudentMistakesResponse> GetAsync(
        string courseId, string studentId, StudentMistakeFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new StudentMistakeFilter();
        var records = await _records.CollectAsync(courseId, studentId, cancellationToken);
        var mistakes = records.Mistakes;
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);
        var classroom = (await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .Where(s => s.StudentId == studentId)
            .ToDictionary(s => s.AssignmentId);

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
            .Select(g => new MistakeAssignmentOption
            {
                AssignmentId = g.Key,
                Title = Title(g.Key),
                DueAt = DueAt(g.Key),
                Count = g.Count(),
            })
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
            .Select(l => new StudentMistakeResponse
            {
                Id = l.Mistake.Id,
                AssignmentId = l.Mistake.AssignmentId,
                AssignmentTitle = Title(l.Mistake.AssignmentId),
                AssignmentDueAt = DueAt(l.Mistake.AssignmentId),
                VersionId = l.Mistake.VersionId,
                ReportId = ReportOf(l.Mistake.ReportId),
                File = l.Mistake.File,
                StartLine = l.Mistake.StartLine,
                EndLine = l.Mistake.EndLine,
                Severity = l.Mistake.Severity,
                Category = l.Mistake.Category,
                Message = l.Mistake.Message,
                Points = l.Mistake.Points,
                Decision = l.Mistake.Decision,
                Label = l.Label,
                RepeatedFromAssignmentTitle = Title(l.FirstAssignmentId),
                ApprovedAt = l.Mistake.ApprovedAt,
                Source = records.SourceOf(l.Mistake.AssignmentId),
            })
            .ToList();

        var categories = MistakeHistory.Trends(selected.Select(l => l.Mistake), DueAt)
            .Select(t => new MistakeCategoryResponse
            {
                Category = t.Category,
                Count = t.Count,
                Percent = Percent(t.Count, total),
                Assignments = t.Assignments,
                Repeated = t.Repeated,
                PointsLost = t.PointsLost,
                FirstAssignmentTitle = Title(t.FirstAssignmentId),
                LastAssignmentTitle = Title(t.LastAssignmentId),
            })
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
                var score = records.Assignments.GetValueOrDefault(g.Key)?.Score;
                var maxPoints = assignments.GetValueOrDefault(g.Key)?.MaxPoints ?? score?.MaxScore;
                return new MistakeAssignmentResponse
                {
                    AssignmentId = g.Key,
                    Title = Title(g.Key),
                    DueAt = DueAt(g.Key),
                    Total = g.Count(),
                    Major = g.Count(l => l.Mistake.Severity == MistakeSeverity.Major),
                    Minor = g.Count(l => l.Mistake.Severity == MistakeSeverity.Minor),
                    Style = g.Count(l => l.Mistake.Severity == MistakeSeverity.Style),
                    Repeated = g.Count(l => l.Label == MistakeLabel.Repeated),
                    PointsLost = g.Sum(l => l.Mistake.Points ?? 0),
                    MaxPoints = maxPoints,
                    ClassroomGrade = submission?.Grade,
                    ReportScore = score?.Score,
                    ReportBonus = score?.Bonus,
                    AiScore = score?.AiScore,
                    AiBonus = score?.AiBonus,
                    Source = records.SourceOf(g.Key),
                };
            })
            .ToList();

        var classroomGraded = byAssignment.Where(a => a is { ClassroomGrade: not null, MaxPoints: > 0 }).ToList();
        double? classroomLost = classroomGraded.Count == 0
            ? null
            : classroomGraded.Sum(a => Math.Max(0, a.MaxPoints!.Value - a.ClassroomGrade!.Value));

        return new StudentMistakesResponse
        {
            Total = total,
            Recorded = mistakes.Count,
            PointsLost = selected.Sum(l => l.Mistake.Points ?? 0),
            ClassroomPointsLost = classroomLost,
            ClassroomGraded = classroomGraded.Count,
            Mistakes = history,
            Categories = categories,
            Severities = severities,
            Labels = labels,
            Assignments = byAssignment,
            AssignmentOptions = options,
        };
    }

    internal static Guid? ReportOf(Guid reportId)
    {
        return reportId == Guid.Empty ? null : reportId;
    }

    private static MistakeShare<T> Share<T>(T key, int count, int total)
    {
        return new MistakeShare<T> { Key = key, Count = count, Percent = Percent(count, total) };
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
    }
}
