using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ClusterMemberResponse
{
    public required Guid Id { get; init; }
    public required Guid GradingMistakeId { get; init; }
    public required Guid VersionId { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string SubmissionId { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public double? Points { get; init; }
    public required string Message { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
}
