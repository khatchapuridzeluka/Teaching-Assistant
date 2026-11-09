using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

public record LatestVersionRow(
    Guid VersionId,
    int VersionNumber,
    VersionStatus VersionStatus,
    DateTimeOffset FetchedAt,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string SubmissionId,
    string StudentId,
    string StudentName,
    string? State,
    bool Late);

public record DraftRow(
    Guid GradingId,
    Guid VersionId,
    int VersionNumber,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string SubmissionId,
    string StudentId,
    string StudentName,
    double? Score,
    double? MaxScore,
    double? Bonus,
    int Major,
    int Minor,
    int Style,
    DateTimeOffset CreatedAt,
    ReportStatus? ReportStatus);
