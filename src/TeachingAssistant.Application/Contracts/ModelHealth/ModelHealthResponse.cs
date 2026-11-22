namespace TeachingAssistant.Application.Contracts;

public record ModelHealthResponse
{
    public required bool Healthy { get; init; }
    public required string Model { get; init; }
    public required long LatencyMs { get; init; }
    public string? Reply { get; init; }
    public string? Error { get; init; }
}
