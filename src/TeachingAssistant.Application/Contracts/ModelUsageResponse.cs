using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

public record ModelUsageResponse(
    DateTimeOffset Since,
    ModelCallPurpose? Purpose,
    ModelUsageSummary Summary,
    IReadOnlyList<ModelUsageTotal> Totals,
    IReadOnlyList<ModelUsageDay> Days,
    IReadOnlyList<ModelCallResponse> Recent);

public record ModelUsageTotal(
    ModelCallPurpose Purpose,
    int Calls,
    int Failed,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long AveragePromptTokens,
    long AverageOutputTokens,
    double? CacheHitRate,
    long AverageDurationMs);

public record ModelUsageSummary(
    int Calls,
    int Failed,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long AveragePromptTokens,
    long AverageOutputTokens,
    double? CacheHitRate,
    long AverageDurationMs);

public record ModelUsageDay(
    DateOnly Date,
    int Calls,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens);

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
