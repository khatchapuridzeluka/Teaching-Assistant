using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>A grading made for an evaluation run: what the model said, never saved as a draft.</summary>
public record EvaluationGrading(
    string AnsweredModel,
    double? Score,
    double? MaxScore,
    double? Bonus,
    IReadOnlyList<RunMistake> Mistakes,
    BuildStatus BuildStatus);
