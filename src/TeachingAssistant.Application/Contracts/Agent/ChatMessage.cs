namespace TeachingAssistant.Application.Contracts;

public record ChatMessage
{
    public required ChatRole Role { get; init; }
    public required string Text { get; init; }
}
