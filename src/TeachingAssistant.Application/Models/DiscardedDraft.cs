using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>A discarded draft with where it belongs and why it was discarded.</summary>
public record DiscardedDraft
{
    public required Guid GradingId { get; init; }
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string Model { get; init; }
    public double? Score { get; init; }
    public double? Bonus { get; init; }
    public double? MaxScore { get; init; }
    public required int Major { get; init; }
    public required int Minor { get; init; }
    public required int Style { get; init; }
    public DiscardReason? Reason { get; init; }
    public string? Note { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DiscardedAt { get; init; }
    public ReportStatus? ReportStatus { get; init; }
}
