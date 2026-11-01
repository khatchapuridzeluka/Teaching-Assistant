namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// Result of a test call to the grading model. Reply is null and Error is set when the call failed.
/// </summary>
public record ModelHealthResponse(
    bool Healthy,
    string Model,
    long LatencyMs,
    string? Reply,
    string? Error);
