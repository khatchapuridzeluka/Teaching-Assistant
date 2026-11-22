namespace TeachingAssistant.Application.Contracts;

public record ChatResponse
{
    public required string Text { get; init; }
}
