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

    /// <summary>Upper limit for one chat reply; chat answers are longer than the short internal prompts. Set in appsettings.json.</summary>
    public int ChatMaxTokens { get; set; } = 8192;

    /// <summary>Model for grading: judging class design needs the strongest model, unlike chat. Set in appsettings.json.</summary>
    public string GradingModel { get; set; } = "claude-opus-5-5";

    /// <summary>Upper limit for one grading reply, including the model's thinking. Set in appsettings.json.</summary>
    public int GradingMaxTokens { get; set; } = 16000;
}
