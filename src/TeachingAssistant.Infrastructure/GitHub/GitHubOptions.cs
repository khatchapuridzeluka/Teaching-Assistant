namespace TeachingAssistant.Infrastructure.GitHub;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>
    /// Optional fine-grained personal access token with read-only access to public repositories. Without it GitHub allows
    /// about 60 requests an hour; with it 5,000. Kept in user-secrets (GitHub:Token), never in appsettings.
    /// </summary>
    public string? Token { get; set; }

    public string ApiBaseUrl { get; set; } = "https://api.github.com/";
}
