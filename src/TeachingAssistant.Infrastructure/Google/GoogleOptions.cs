namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// Settings bound from the "Google" configuration section.
/// ClientId and ClientSecret identify this app's OAuth client (type "Desktop app") and come from user-secrets.
/// </summary>
public class GoogleOptions
{
    public const string SectionName = "Google";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Folder under %APPDATA% where the teacher's token is stored (plain JSON; fine for local development only).
    /// </summary>
    public string TokenStoreFolder { get; set; } = "TeachingAssistant.Google";
}
