using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record SubmissionResponse(
    string Id,
    string AssignmentId,
    string StudentId,
    string StudentName,
    string State,
    bool Late,
    DateTimeOffset? TurnedInAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<SubmissionAttachment> Attachments,
    string? Link,
    double? AssignedGrade,
    IReadOnlyList<GradeChange> GradeHistory);

public record GradeChange(GradeEventKind Kind, double? PointsEarned, double? MaxPoints, DateTimeOffset At);

public record SubmissionAttachment(
    AttachmentKind Kind,
    string Title,
    string? Url,
    string? FileId);

public enum AttachmentKind
{
    DriveFile,

    Link,

    Other,
}
