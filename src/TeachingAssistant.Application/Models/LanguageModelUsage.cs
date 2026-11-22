namespace TeachingAssistant.Application.Models;

public record LanguageModelUsage
{
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
}
