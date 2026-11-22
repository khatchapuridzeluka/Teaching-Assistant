namespace TeachingAssistant.Application.Contracts;

public record ApproveExplanationRequest
{
    public required string Explanation { get; init; }
}
