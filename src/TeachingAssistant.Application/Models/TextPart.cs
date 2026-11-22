namespace TeachingAssistant.Application.Models;

public sealed record TextPart : PromptPart
{
    public required string Text { get; init; }
}
