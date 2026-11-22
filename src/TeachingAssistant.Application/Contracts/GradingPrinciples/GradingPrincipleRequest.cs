namespace TeachingAssistant.Application.Contracts;

public record GradingPrincipleRequest
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
}
