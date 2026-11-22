namespace TeachingAssistant.Application.Models;

public record LanguageModelReply
{
    public required string Model { get; init; }
    public required string Text { get; init; }
    public LanguageModelUsage? Usage { get; init; }
}
