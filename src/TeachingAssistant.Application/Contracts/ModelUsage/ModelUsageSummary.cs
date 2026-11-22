namespace TeachingAssistant.Application.Contracts;

public record ModelUsageSummary
{
    public required int Calls { get; init; }
    public required int Failed { get; init; }
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
    public required long AveragePromptTokens { get; init; }
    public required long AverageOutputTokens { get; init; }
    public double? CacheHitRate { get; init; }
    public required long AverageDurationMs { get; init; }
}
