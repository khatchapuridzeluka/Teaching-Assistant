namespace TeachingAssistant.Infrastructure.LanguageModel;

/// <summary>
/// Settings bound from the "Anthropic" configuration section.
/// ApiKey comes from user-secrets (dev) or a secret store (prod), never from appsettings.json.
/// If ApiKey is empty, the SDK falls back to the ANTHROPIC_API_KEY environment variable.
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-5-5";
    public int MaxTokens { get; set; } = 1024;
}
