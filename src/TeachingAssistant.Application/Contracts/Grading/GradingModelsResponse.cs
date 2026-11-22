namespace TeachingAssistant.Application.Contracts;

/// <summary>The models a version can be tried with.</summary>
public record GradingModelsResponse
{
    /// <summary>Every model "Try with model" and evaluation runs can use, most capable first.</summary>
    public required IReadOnlyList<string> Available { get; init; }

    /// <summary>The model that grades drafts now.</summary>
    public required string Current { get; init; }
}
