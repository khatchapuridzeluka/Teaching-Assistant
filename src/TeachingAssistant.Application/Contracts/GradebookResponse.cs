namespace TeachingAssistant.Application.Contracts;

public record GradebookResponse(
    DateTimeOffset? SyncedAt,
    IReadOnlyList<GradebookColumn> Assignments,
    IReadOnlyList<CourseStudentResponse> Students,
    double? ClassAveragePercent);

public record GradebookColumn(
    string Id,
    string Title,
    double? MaxPoints,
    DateTimeOffset? DueAt,
    int Graded,
    int AwaitingGrade,
    int Missing,
    double? AveragePercent);
