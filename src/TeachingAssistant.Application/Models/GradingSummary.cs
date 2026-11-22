using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public record GradingSummary
{
    public required Guid Id { get; init; }
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required string AssignmentId { get; init; }
    public required string SubmissionId { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public required GradingStatus Status { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public required int Major { get; init; }
    public required int Minor { get; init; }
    public required int Style { get; init; }
    public required string Model { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public ReportStatus? ReportStatus { get; init; }
}
