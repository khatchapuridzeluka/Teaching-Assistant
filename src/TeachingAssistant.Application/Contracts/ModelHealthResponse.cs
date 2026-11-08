namespace TeachingAssistant.Application.Contracts;

public record ModelHealthResponse(
    bool Healthy,
    string Model,
    long LatencyMs,
    string? Reply,
    string? Error);
