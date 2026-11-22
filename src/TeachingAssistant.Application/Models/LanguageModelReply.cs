namespace TeachingAssistant.Application.Models;

public record LanguageModelReply
{
    public required string Model { get; init; }
    public required string Text { get; init; }
    public LanguageModelUsage? Usage { get; init; }
}

public record LanguageModelUsage
{
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }
    public required long CacheReadTokens { get; init; }
    public required long CacheWriteTokens { get; init; }
}
