using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public record GradingSummary(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    string AssignmentId,
    string SubmissionId,
    string StudentId,
    string StudentName,
    GradingStatus Status,
    double? Score,
    double? MaxScore,
    double? Bonus,
    int Major,
    int Minor,
    int Style,
    string Model,
    DateTimeOffset CreatedAt,
    ReportStatus? ReportStatus);
