using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Models;

public record StudentRecordAssignment(string AssignmentId, StudentRecordSource Source, ReportScore? Score);

public record StudentRecords(
    IReadOnlyList<StudentMistake> Mistakes,
    IReadOnlyList<StudentStrength> Strengths,
    IReadOnlyDictionary<string, StudentRecordAssignment> Assignments)
{
    public StudentRecordSource SourceOf(string assignmentId)
    {
        return Assignments.TryGetValue(assignmentId, out var assignment) ? assignment.Source : StudentRecordSource.Approved;
    }
}
