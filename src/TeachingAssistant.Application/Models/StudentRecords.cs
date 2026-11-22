using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Models;

public record StudentRecords
{
    public required IReadOnlyList<StudentMistake> Mistakes { get; init; }
    public required IReadOnlyList<StudentStrength> Strengths { get; init; }
    public required IReadOnlyDictionary<string, StudentRecordAssignment> Assignments { get; init; }

    public StudentRecordSource SourceOf(string assignmentId)
    {
        return Assignments.TryGetValue(assignmentId, out var assignment) ? assignment.Source : StudentRecordSource.Approved;
    }
}
