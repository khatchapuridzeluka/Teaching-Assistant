using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One discarded draft: whose and which assignment, the model, the AI's total (score plus bonus) and mistakes per severity,
/// why it was discarded and the teacher's note, and the status its report had, if one was made.
/// </summary>
public record DiscardedRow
{
    public required Guid GradingId { get; init; }
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string Model { get; init; }
    public double? AiScore { get; init; }
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
