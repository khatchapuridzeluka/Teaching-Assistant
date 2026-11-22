namespace TeachingAssistant.Application.Contracts;

/// <summary>Grade a version once with this model; the answer is shown, never saved.</summary>
public record TryModelRequest
{
    public required string Model { get; init; }
}
