using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

public record ModelCallResponse
{
    public required Guid Id { get; init; }
    public required ModelCallPurpose Purpose { get; init; }
    public required string RequestedModel { get; init; }
    public string? Model { get; init; }
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public long? CacheReadTokens { get; init; }
    public long? CacheWriteTokens { get; init; }
    public required long DurationMs { get; init; }
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public Guid? VersionId { get; init; }
    public string? SubmissionId { get; init; }
    public string? StudentId { get; init; }
    public string? StudentName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
