namespace TeachingAssistant.Application.Contracts;

public record EvaluationScores
{
    /// <summary>Reports where both totals and the maximum are known.</summary>
    public required int Compared { get; init; }

    /// <summary>100 minus the average gap, in percent: 97.8 when the AI is off by 2.2 points per 100 on average.</summary>
    public double? Accuracy { get; init; }

    /// <summary>How close counts as agreeing, in percent of the maximum (5 = 5 points out of 100).</summary>
    public required double TolerancePercent { get; init; }

    /// <summary>How many of them agree within the tolerance.</summary>
    public required int WithinTolerance { get; init; }

    /// <summary>How many have the very same total.</summary>
    public required int Exact { get; init; }

    /// <summary>Average size of the difference, in percent of the maximum.</summary>
    public double? MeanAbsoluteDifference { get; init; }

    /// <summary>Average signed difference, in percent of the maximum: positive means the AI is more lenient.</summary>
    public double? MeanDifference { get; init; }

    /// <summary>How many times the AI gave more than the teacher.</summary>
    public required int AiHigher { get; init; }

    /// <summary>How many times the AI gave less than the teacher.</summary>
    public required int AiLower { get; init; }

    /// <summary>How the differences spread, in 5-point steps of percent of the maximum.</summary>
    public required IReadOnlyList<DifferenceBucket> Buckets { get; init; }
}
