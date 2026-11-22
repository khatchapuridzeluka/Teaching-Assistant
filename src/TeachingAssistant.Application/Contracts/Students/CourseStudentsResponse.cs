namespace TeachingAssistant.Application.Contracts;

public record CourseStudentsResponse
{
    public DateTimeOffset? SyncedAt { get; init; }
    public required IReadOnlyList<StudentAssignmentColumn> Assignments { get; init; }
    public required IReadOnlyList<CourseStudentResponse> Students { get; init; }
}
