namespace TeachingAssistant.Application.Contracts;

public record CourseStudentsResponse(
    DateTimeOffset? SyncedAt,
    IReadOnlyList<StudentAssignmentColumn> Assignments,
    IReadOnlyList<CourseStudentResponse> Students);
