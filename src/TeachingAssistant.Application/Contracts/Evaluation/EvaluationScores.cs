namespace TeachingAssistant.Application.Contracts;

/// <param name="Compared">Reports where both totals and the maximum are known.</param>
/// <param name="Accuracy">100 minus the average gap, in percent: 97.8 when the AI is off by 2.2 points per 100 on average.</param>
/// <param name="TolerancePercent">How close counts as agreeing, in percent of the maximum (5 = 5 points out of 100).</param>
/// <param name="WithinTolerance">How many of them agree within the tolerance.</param>
/// <param name="Exact">How many have the very same total.</param>
/// <param name="MeanAbsoluteDifference">Average size of the difference, in percent of the maximum.</param>
/// <param name="MeanDifference">Average signed difference, in percent of the maximum: positive means the AI is more lenient.</param>
/// <param name="AiHigher">How many times the AI gave more than the teacher.</param>
/// <param name="AiLower">How many times the AI gave less than the teacher.</param>
/// <param name="Buckets">How the differences spread, in 5-point steps of percent of the maximum.</param>
public record EvaluationScores(
    int Compared,
    double? Accuracy,
    double TolerancePercent,
    int WithinTolerance,
    int Exact,
    double? MeanAbsoluteDifference,
    double? MeanDifference,
    int AiHigher,
    int AiLower,
    IReadOnlyList<DifferenceBucket> Buckets);
