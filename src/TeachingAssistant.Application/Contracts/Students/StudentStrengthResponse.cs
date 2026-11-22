using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthResponse
{
    public required Guid Id { get; init; }
    public required string AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public DateTimeOffset? AssignmentDueAt { get; init; }
    public required Guid VersionId { get; init; }
    public Guid? ReportId { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public required MistakeDecision Decision { get; init; }
    public required StrengthLabel Label { get; init; }
    public string? SinceAssignmentTitle { get; init; }
    public required DateTimeOffset ApprovedAt { get; init; }
    public required StudentRecordSource Source { get; init; }
}
