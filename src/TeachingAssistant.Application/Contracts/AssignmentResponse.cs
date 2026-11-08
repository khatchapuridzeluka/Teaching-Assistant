namespace TeachingAssistant.Application.Contracts;

public record AssignmentResponse(
    string Id,
    string CourseId,
    string Title,
    string? Description,
    string WorkType,
    double? MaxPoints,
    DateTimeOffset? DueAt,
    DateTimeOffset? CreatedAt,
    string? Link,
    IReadOnlyList<SubmissionAttachment> Materials);
