namespace TeachingAssistant.Application.Contracts;

public record ModelUsageDay(
    DateOnly Date,
    int Calls,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens);
