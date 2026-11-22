namespace TeachingAssistant.Application.Contracts;

public record JobOverviewItem(
    JobResponse Job,
    string? CourseId,
    string? AssignmentId,
    string? AssignmentTitle,
    string? SubmissionId,
    string? StudentId,
    string? StudentName,
    int? VersionNumber,
    JobChildCounts? Children = null);
