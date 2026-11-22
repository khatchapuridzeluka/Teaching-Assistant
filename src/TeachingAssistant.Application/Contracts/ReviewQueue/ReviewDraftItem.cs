using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ReviewDraftItem(
    Guid GradingId,
    Guid VersionId,
    int VersionNumber,
    bool IsLatestVersion,
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
