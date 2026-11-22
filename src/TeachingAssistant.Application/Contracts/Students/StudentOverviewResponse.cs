namespace TeachingAssistant.Application.Contracts;

public record StudentOverviewResponse
{
    public required int GradedAssignments { get; init; }
    public double? AveragePercent { get; init; }
    public double? FirstPercent { get; init; }
    public double? LatestPercent { get; init; }
    public required int Mistakes { get; init; }
    public required int RepeatedMistakes { get; init; }
    public required double PointsLost { get; init; }
    public required int Strengths { get; init; }
    public required int ConsistentStrengths { get; init; }
    public required int ImprovedStrengths { get; init; }
    public required IReadOnlyList<OverviewAssignmentResponse> Assignments { get; init; }
    public required IReadOnlyList<MistakeCategoryResponse> TopMistakes { get; init; }
    public required IReadOnlyList<StrengthCategoryResponse> TopStrengths { get; init; }
    public required IReadOnlyList<OverviewCategoryResponse> Categories { get; init; }
}
