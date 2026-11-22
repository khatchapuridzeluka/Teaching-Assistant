using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>A grading made for an evaluation run: what the model said, never saved as a draft.</summary>
public record EvaluationGrading
{
    public required string AnsweredModel { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public required IReadOnlyList<RunMistake> Mistakes { get; init; }
    public required BuildStatus BuildStatus { get; init; }
}
