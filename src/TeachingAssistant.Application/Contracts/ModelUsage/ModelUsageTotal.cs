using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Contracts;

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
