namespace TeachingAssistant.Application.Contracts;

public record EvaluationMistakes
{
    /// <summary>The AI's mistakes in the compared reports.</summary>
    public required int Total { get; init; }

    /// <summary>Kept as the AI wrote them.</summary>
    public required int Kept { get; init; }

    /// <summary>Kept but changed (message, severity, category or points).</summary>
    public required int Edited { get; init; }

    /// <summary>Rejected: not a real mistake according to the teacher.</summary>
    public required int Rejected { get; init; }

    /// <summary>Kept mistakes whose points the teacher changed.</summary>
    public required int PointsChanged { get; init; }

    /// <summary>Average size of those point changes.</summary>
    public double? MeanPointsChange { get; init; }

    /// <summary>The decisions per category the AI chose.</summary>
    public required IReadOnlyList<CategoryDecisions> ByCategory { get; init; }

    /// <summary>The decisions per severity the AI chose.</summary>
    public required IReadOnlyList<SeverityDecisions> BySeverity { get; init; }
}
