namespace TeachingAssistant.Application.Contracts;

public record EvaluationStrengths
{
    public required int Total { get; init; }
    public required int Kept { get; init; }
    public required int Edited { get; init; }
    public required int Rejected { get; init; }
}
