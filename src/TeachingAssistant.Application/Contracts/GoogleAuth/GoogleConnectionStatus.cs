namespace TeachingAssistant.Application.Contracts;

public record GoogleConnectionStatus(
    GoogleConnectionState State,
    IReadOnlyList<string> GrantedScopes,
    string? Message);
