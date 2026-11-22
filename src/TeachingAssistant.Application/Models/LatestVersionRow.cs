using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

public record LatestVersionRow
{
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required VersionStatus VersionStatus { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public required string SubmissionId { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public string? State { get; init; }
    public required bool Late { get; init; }
}
