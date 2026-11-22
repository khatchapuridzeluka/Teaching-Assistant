using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Models;

public record StudentRecordAssignment
{
    public required string AssignmentId { get; init; }
    public required StudentRecordSource Source { get; init; }
    public ReportScore? Score { get; init; }
}
