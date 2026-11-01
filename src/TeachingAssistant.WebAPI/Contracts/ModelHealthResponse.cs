namespace TeachingAssistant.WebAPI.Contracts;

/// <summary>
/// Result of a test call to the grading model.
/// </summary>
/// <param name="Healthy">True if the model answered the test prompt.</param>
/// <param name="Model">The model ID that was called, e.g. <c>claude-haiku-5-5</c>.</param>
/// <param name="LatencyMs">Round-trip time of the test call in milliseconds.</param>
/// <param name="Reply">The model's answer to the test prompt; null when the call failed.</param>
/// <param name="Error">What went wrong when <paramref name="Healthy"/> is false; otherwise null.</param>
public record ModelHealthResponse(
    bool Healthy,
    string Model,
    long LatencyMs,
    string? Reply,
    string? Error);
