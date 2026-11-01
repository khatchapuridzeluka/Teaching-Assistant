namespace TeachingAssistant.Application.Models;

/// <summary>
/// Text reply from the model, with the ID of the model that produced it.
/// </summary>
public record LanguageModelReply(string Model, string Text);
