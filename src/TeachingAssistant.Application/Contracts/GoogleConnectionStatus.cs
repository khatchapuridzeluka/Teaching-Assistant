namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// Whether the app can read Google Classroom and Drive on the teacher's behalf.
/// Message explains what to do when the state is not Connected.
/// </summary>
public record GoogleConnectionStatus(
    GoogleConnectionState State,
    IReadOnlyList<string> GrantedScopes,
    string? Message);

/// <summary>
/// Connection state of the teacher's Google account.
/// </summary>
public enum GoogleConnectionState
{
    /// <summary>Google client ID/secret are missing from configuration.</summary>
    NotConfigured,

    /// <summary>No stored authorization; connect first.</summary>
    NotConnected,

    /// <summary>Authorized with all required read-only scopes.</summary>
    Connected,

    /// <summary>Stored authorization expired, was revoked, or lacks a required scope; connect again.</summary>
    ReconnectRequired,
}
