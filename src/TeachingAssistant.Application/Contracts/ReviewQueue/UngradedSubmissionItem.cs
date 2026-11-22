namespace TeachingAssistant.Application.Contracts;

public record UngradedSubmissionItem(
    Guid VersionId,
    int VersionNumber,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string SubmissionId,
    string StudentId,
    string StudentName,
    bool Late,
    DateTimeOffset FetchedAt);
