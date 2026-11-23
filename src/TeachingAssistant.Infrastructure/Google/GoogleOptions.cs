namespace TeachingAssistant.Infrastructure.Google;

public class GoogleOptions
{
    public const string SectionName = "Google";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public string? RedirectUri { get; set; }

    public string TokenStoreFolder { get; set; } = "TeachingAssistant.Google";
}
