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
