using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentStrengthService : IStudentStrengthService
{
    private readonly StudentRecordCollector _records;
    private readonly ICourseAssignmentStore _assignments;

    public StudentStrengthService(StudentRecordCollector records, ICourseAssignmentStore assignments)
    {
        _records = records;
        _assignments = assignments;
    }

    public async Task<StudentStrengthsResponse> GetAsync(
        string courseId, string studentId, StudentStrengthFilter? filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new StudentStrengthFilter();
        var records = await _records.CollectAsync(courseId, studentId, cancellationToken);
        var (mistakes, strengths) = (records.Mistakes, records.Strengths);
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);

        DateTimeOffset? DueAt(string assignmentId)
        {
            return assignments.GetValueOrDefault(assignmentId)?.DueAt;
        }

        string? Title(string? assignmentId)
        {
            return assignmentId is null ? null : assignments.GetValueOrDefault(assignmentId)?.Title;
        }

        var labeled = StrengthHistory.Label(strengths, mistakes, DueAt);
        var options = labeled
            .GroupBy(l => l.Strength.AssignmentId)
            .Select(g => new MistakeAssignmentOption(g.Key, Title(g.Key), DueAt(g.Key), g.Count()))
            .ToList();

        var selected = labeled
            .Where(l => filter.AssignmentId is null || l.Strength.AssignmentId == filter.AssignmentId)
            .Where(l => filter.Category is null || l.Strength.Category == filter.Category)
            .Where(l => filter.Label is null || l.Label == filter.Label)
            .ToList();
        var total = selected.Count;

        var list = selected
            .AsEnumerable()
            .Reverse()
            .Select(l => new StudentStrengthResponse(l.Strength.Id, l.Strength.AssignmentId, Title(l.Strength.AssignmentId),
                DueAt(l.Strength.AssignmentId), l.Strength.VersionId, StudentMistakeService.ReportOf(l.Strength.ReportId), l.Strength.File,
                l.Strength.StartLine, l.Strength.EndLine, l.Strength.Category, l.Strength.Message, l.Strength.Decision, l.Label,
                Title(l.SinceAssignmentId), l.Strength.ApprovedAt, records.SourceOf(l.Strength.AssignmentId)))
            .ToList();

        var categories = StrengthHistory.Trends(selected.Select(l => l.Strength), mistakes, DueAt)
            .Select(t => new StrengthCategoryResponse(t.Category, t.Count, Percent(t.Count, total), t.Assignments, t.Consistent,
                t.Mistakes, Title(t.ImprovedSinceAssignmentId), Title(t.FirstAssignmentId), Title(t.LastAssignmentId)))
            .ToList();

        var labels = Enum.GetValues<StrengthLabel>()
            .Select(label => new MistakeShare<StrengthLabel>(label, selected.Count(l => l.Label == label),
                Percent(selected.Count(l => l.Label == label), total)))
            .ToList();

        var shownMistakes = mistakes
            .Where(m => filter.AssignmentId is null || m.AssignmentId == filter.AssignmentId)
            .Where(m => filter.Category is null || m.Category == filter.Category)
            .ToList();
        var byAssignment = selected.Select(l => l.Strength.AssignmentId)
            .Concat(filter.Label is null ? shownMistakes.Select(m => m.AssignmentId) : [])
            .Distinct()
            .OrderBy(id => DueAt(id) ?? DateTimeOffset.MaxValue)
            .ThenBy(id => id, StringComparer.Ordinal)
            .Select(id => new StrengthAssignmentResponse(id, Title(id), DueAt(id),
                selected.Count(l => l.Strength.AssignmentId == id), shownMistakes.Count(m => m.AssignmentId == id)))
            .ToList();

        return new StudentStrengthsResponse(total, strengths.Count, mistakes.Count, list, categories, labels, byAssignment, options);
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
    }
}
