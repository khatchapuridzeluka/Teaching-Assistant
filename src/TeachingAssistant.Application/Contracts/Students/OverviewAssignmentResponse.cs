namespace TeachingAssistant.Application.Contracts;

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
