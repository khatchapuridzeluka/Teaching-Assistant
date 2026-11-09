namespace TeachingAssistant.Application.Models;

public record JobSubject(
    Guid VersionId,
    int VersionNumber,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string SubmissionId,
    string StudentId,
    string StudentName);
