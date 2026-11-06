namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// A published assignment in a course. Description is the task statement as written in the source.
/// DueAt is in UTC and null when the assignment has no due date. MaxPoints is null for ungraded work.
/// Materials are the files and links the teacher attached to the assignment, usually the instructions and grading
/// scheme (e.g. a PDF); the grader reads them.
/// </summary>
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
