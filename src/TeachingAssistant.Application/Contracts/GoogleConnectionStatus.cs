namespace TeachingAssistant.Application.Contracts;

public record GoogleConnectionStatus(
    GoogleConnectionState State,
    IReadOnlyList<string> GrantedScopes,
    string? Message);

public enum GoogleConnectionState
{
    NotConfigured,

    NotConnected,

    Connected,

    ReconnectRequired,
}
