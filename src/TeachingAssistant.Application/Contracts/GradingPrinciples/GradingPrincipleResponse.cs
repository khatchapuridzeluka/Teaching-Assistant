namespace TeachingAssistant.Application.Contracts;

public record GradingPrincipleResponse
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
