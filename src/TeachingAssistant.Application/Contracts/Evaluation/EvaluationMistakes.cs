namespace TeachingAssistant.Application.Contracts;

/// <param name="Total">The AI's mistakes in the compared reports.</param>
/// <param name="Kept">Kept as the AI wrote them.</param>
/// <param name="Edited">Kept but changed (message, severity, category or points).</param>
/// <param name="Rejected">Rejected: not a real mistake according to the teacher.</param>
/// <param name="PointsChanged">Kept mistakes whose points the teacher changed.</param>
/// <param name="MeanPointsChange">Average size of those point changes.</param>
/// <param name="ByCategory">The decisions per category the AI chose.</param>
/// <param name="BySeverity">The decisions per severity the AI chose.</param>
public record EvaluationMistakes(
    int Total,
    int Kept,
    int Edited,
    int Rejected,
    int PointsChanged,
    double? MeanPointsChange,
    IReadOnlyList<CategoryDecisions> ByCategory,
    IReadOnlyList<SeverityDecisions> BySeverity);
