namespace TeachingAssistant.Application.Contracts;

public record GoogleConnectionStatus
{
    public required GoogleConnectionState State { get; init; }
    public required IReadOnlyList<string> GrantedScopes { get; init; }
    public string? Message { get; init; }
}
