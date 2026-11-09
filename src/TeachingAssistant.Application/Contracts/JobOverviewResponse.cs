namespace TeachingAssistant.Application.Contracts;

public record JobOverviewResponse(JobCounts Counts, IReadOnlyList<JobOverviewItem> Jobs);

public record JobCounts(int Queued, int Running, int Succeeded, int Failed, int Canceled, DateTimeOffset FinishedSince);

public record JobOverviewItem(
    JobResponse Job,
    string? CourseId,
    string? AssignmentId,
    string? AssignmentTitle,
    string? SubmissionId,
    string? StudentId,
    string? StudentName,
    int? VersionNumber);
