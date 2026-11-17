using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record CourseStudentsResponse(
    DateTimeOffset? SyncedAt,
    IReadOnlyList<StudentAssignmentColumn> Assignments,
    IReadOnlyList<CourseStudentResponse> Students);

public record StudentAssignmentColumn(string Id, string Title, double? MaxPoints, DateTimeOffset? DueAt);

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

public record StudentAssignmentResult(
    string AssignmentId,
    string? SubmissionId,
    string? State,
    bool TurnedIn,
    bool Late,
    bool Missing,
    GradeStatus GradeStatus,
    double? Grade);
