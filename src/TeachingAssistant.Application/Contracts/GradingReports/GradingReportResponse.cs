using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingReportResponse(
    Guid Id,
    Guid GradingId,
    Guid VersionId,
    string StudentId,
    string CourseId,
    string AssignmentId,
    ReportStatus Status,
    double? Score,
    double? MaxScore,
    double? Bonus,
    string Review,
    IReadOnlyList<ReportMistakeResponse> Mistakes,
    IReadOnlyList<ReportStrengthResponse> Strengths,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ApprovedAt);
