namespace TeachingAssistant.Application.Contracts;

public record StudentOverviewResponse(
    int GradedAssignments,
    double? AveragePercent,
    double? FirstPercent,
    double? LatestPercent,
    int Mistakes,
    int RepeatedMistakes,
    double PointsLost,
    int Strengths,
    int ConsistentStrengths,
    int ImprovedStrengths,
    IReadOnlyList<OverviewAssignmentResponse> Assignments,
    IReadOnlyList<MistakeCategoryResponse> TopMistakes,
    IReadOnlyList<StrengthCategoryResponse> TopStrengths,
    IReadOnlyList<OverviewCategoryResponse> Categories);
