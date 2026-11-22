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
            .Select(g => new MistakeAssignmentOption
            {
                AssignmentId = g.Key,
                Title = Title(g.Key),
                DueAt = DueAt(g.Key),
                Count = g.Count(),
            })
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
            .Select(l => new StudentStrengthResponse
            {
                Id = l.Strength.Id,
                AssignmentId = l.Strength.AssignmentId,
                AssignmentTitle = Title(l.Strength.AssignmentId),
                AssignmentDueAt = DueAt(l.Strength.AssignmentId),
                VersionId = l.Strength.VersionId,
                ReportId = StudentMistakeService.ReportOf(l.Strength.ReportId),
                File = l.Strength.File,
                StartLine = l.Strength.StartLine,
                EndLine = l.Strength.EndLine,
                Category = l.Strength.Category,
                Message = l.Strength.Message,
                Decision = l.Strength.Decision,
                Label = l.Label,
                SinceAssignmentTitle = Title(l.SinceAssignmentId),
                ApprovedAt = l.Strength.ApprovedAt,
                Source = records.SourceOf(l.Strength.AssignmentId),
            })
            .ToList();

        var categories = StrengthHistory.Trends(selected.Select(l => l.Strength), mistakes, DueAt)
            .Select(t => new StrengthCategoryResponse
            {
                Category = t.Category,
                Count = t.Count,
                Percent = Percent(t.Count, total),
                Assignments = t.Assignments,
                Consistent = t.Consistent,
                Mistakes = t.Mistakes,
                ImprovedSinceAssignmentTitle = Title(t.ImprovedSinceAssignmentId),
                FirstAssignmentTitle = Title(t.FirstAssignmentId),
                LastAssignmentTitle = Title(t.LastAssignmentId),
            })
            .ToList();

        var labels = Enum.GetValues<StrengthLabel>()
            .Select(label => new MistakeShare<StrengthLabel>
            {
                Key = label,
                Count = selected.Count(l => l.Label == label),
                Percent = Percent(selected.Count(l => l.Label == label), total),
            })
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
            .Select(id => new StrengthAssignmentResponse
            {
                AssignmentId = id,
                Title = Title(id),
                DueAt = DueAt(id),
                Strengths = selected.Count(l => l.Strength.AssignmentId == id),
                Mistakes = shownMistakes.Count(m => m.AssignmentId == id),
            })
            .ToList();

        return new StudentStrengthsResponse
        {
            Total = total,
            Recorded = strengths.Count,
            RecordedMistakes = mistakes.Count,
            Strengths = list,
            Categories = categories,
            Labels = labels,
            Assignments = byAssignment,
            AssignmentOptions = options,
        };
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
    }
}
