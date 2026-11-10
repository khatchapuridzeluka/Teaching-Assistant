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
    int? VersionNumber,
    JobChildCounts? Children = null);

public record JobChildCounts(int Total, int Queued, int Running, int Succeeded, int Failed, int Canceled);

public record GradeAllResponse(
    JobResponse? Job,
    int Queued,
    int Students,
    int AlreadyDrafted,
    int AlreadyQueued,
    int NoWork,
    int NotFetched,
    IReadOnlyList<string> Problems);
