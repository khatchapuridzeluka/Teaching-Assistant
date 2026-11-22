namespace TeachingAssistant.Application.Contracts;

public record JobOverviewItem
{
    public required JobResponse Job { get; init; }
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public string? SubmissionId { get; init; }
    public string? StudentId { get; init; }
    public string? StudentName { get; init; }
    public int? VersionNumber { get; init; }
    public JobChildCounts? Children { get; init; }
}
