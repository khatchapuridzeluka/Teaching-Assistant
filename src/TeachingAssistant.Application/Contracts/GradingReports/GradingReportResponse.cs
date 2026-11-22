using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingReportResponse
{
    public required Guid Id { get; init; }
    public required Guid GradingId { get; init; }
    public required Guid VersionId { get; init; }
    public required string StudentId { get; init; }
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public required ReportStatus Status { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public required string Review { get; init; }
    public required IReadOnlyList<ReportMistakeResponse> Mistakes { get; init; }
    public required IReadOnlyList<ReportStrengthResponse> Strengths { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
}
