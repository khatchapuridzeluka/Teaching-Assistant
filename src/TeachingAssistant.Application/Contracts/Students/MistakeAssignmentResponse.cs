namespace TeachingAssistant.Application.Contracts;

public record MistakeAssignmentResponse(
    string AssignmentId,
    string? Title,
    DateTimeOffset? DueAt,
    int Total,
    int Major,
    int Minor,
    int Style,
    int Repeated,
    double PointsLost,
    double? MaxPoints,
    double? ClassroomGrade,
    double? ReportScore,
    double? ReportBonus,
    double? AiScore,
    double? AiBonus,
    StudentRecordSource Source);
