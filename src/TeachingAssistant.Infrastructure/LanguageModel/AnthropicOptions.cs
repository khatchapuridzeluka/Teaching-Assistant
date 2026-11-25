namespace TeachingAssistant.Infrastructure.LanguageModel;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-5-5";
    public int MaxTokens { get; set; } = 1024;

    public int ChatMaxTokens { get; set; } = 8192;

    public string GradingModel { get; set; } = "claude-opus-5-5";

    public int GradingMaxTokens { get; set; } = 64000;

    public string AdapterModel { get; set; } = "claude-sonnet-5-5";
}
