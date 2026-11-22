namespace TeachingAssistant.Application.Contracts;

public record ModelUsageDay
{
    public required DateOnly Date { get; init; }
    public required int Calls { get; init; }
    public required long InputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
    public required long OutputTokens { get; init; }
}
