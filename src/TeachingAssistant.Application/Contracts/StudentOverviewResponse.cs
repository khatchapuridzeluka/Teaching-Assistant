using TeachingAssistant.Domain.Grading;

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

public record OverviewAssignmentResponse(
    string AssignmentId,
    string? Title,
    DateTimeOffset? DueAt,
    StudentRecordSource Source,
    double? Score,
    double? Bonus,
    double? MaxPoints,
    double? Percent,
    double? AiScore,
    int Mistakes,
    int Major,
    int Repeated,
    double PointsLost,
    int Strengths,
    int Improved);

public record OverviewCategoryResponse(MistakeCategory Category, int Strengths, int Mistakes, double PointsLost);
