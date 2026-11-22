namespace TeachingAssistant.Application.Contracts;

public record GradebookResponse(
    DateTimeOffset? SyncedAt,
    IReadOnlyList<GradebookColumn> Assignments,
    IReadOnlyList<CourseStudentResponse> Students,
    double? ClassAveragePercent);
