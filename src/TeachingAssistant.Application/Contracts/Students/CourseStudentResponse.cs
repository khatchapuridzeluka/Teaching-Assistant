namespace TeachingAssistant.Application.Contracts;

public record CourseStudentResponse(
    Guid Id,
    string ExternalUserId,
    string Name,
    bool Active,
    int TurnedIn,
    int Missing,
    int Late,
    double? OverallPercent,
    double Earned,
    double Possible,
    IReadOnlyList<StudentAssignmentResult> Results);
