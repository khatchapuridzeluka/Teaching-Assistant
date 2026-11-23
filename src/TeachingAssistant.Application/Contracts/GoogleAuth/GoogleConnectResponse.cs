namespace TeachingAssistant.Application.Contracts;

public record GoogleConnectResponse
{
    public required GoogleConnectionState State { get; init; }

    /// <summary>Google's consent page to send the browser to; missing when already connected or not configured.</summary>
    public string? AuthorizationUrl { get; init; }

    public string? Message { get; init; }
}
