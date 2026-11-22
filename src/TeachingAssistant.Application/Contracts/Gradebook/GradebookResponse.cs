namespace TeachingAssistant.Application.Contracts;

public record GradebookResponse
{
    public DateTimeOffset? SyncedAt { get; init; }
    public required IReadOnlyList<GradebookColumn> Assignments { get; init; }
    public required IReadOnlyList<CourseStudentResponse> Students { get; init; }
    public double? ClassAveragePercent { get; init; }
}
