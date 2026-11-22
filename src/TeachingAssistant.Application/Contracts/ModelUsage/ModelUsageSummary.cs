namespace TeachingAssistant.Application.Contracts;

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
