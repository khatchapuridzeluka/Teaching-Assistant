namespace TeachingAssistant.Application.Contracts;

/// <summary>The models a version can be tried with.</summary>
/// <param name="Available">Every model "Try with model" and evaluation runs can use, most capable first.</param>
/// <param name="Current">The model that grades drafts now.</param>
public record GradingModelsResponse(IReadOnlyList<string> Available, string Current);
