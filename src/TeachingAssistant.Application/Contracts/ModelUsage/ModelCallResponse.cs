using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

public record ModelCallResponse(
    Guid Id,
    ModelCallPurpose Purpose,
    string RequestedModel,
    string? Model,
    bool Succeeded,
    string? Error,
    long? InputTokens,
    long? OutputTokens,
    long? CacheReadTokens,
    long? CacheWriteTokens,
    long DurationMs,
    string? CourseId,
    string? AssignmentId,
    string? AssignmentTitle,
    Guid? VersionId,
    string? SubmissionId,
    string? StudentId,
    string? StudentName,
    DateTimeOffset CreatedAt);
