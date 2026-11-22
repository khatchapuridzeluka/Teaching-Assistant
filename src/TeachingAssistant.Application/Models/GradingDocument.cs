namespace TeachingAssistant.Application.Models;

public record GradingDocument
{
    public required string StudentName { get; init; }
    public required string AssignmentTitle { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required int VersionNumber { get; init; }
    public DateTimeOffset? TurnedInAt { get; init; }
    public required GradingDocumentStatus Status { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public double? AiScore { get; init; }
    public double? AiBonus { get; init; }
    public required string Review { get; init; }
    public required IReadOnlyList<GradingDocumentMistake> Mistakes { get; init; }
    public required IReadOnlyList<GradingDocumentStrength> Strengths { get; init; }
    public required string Model { get; init; }
    public required DateTimeOffset GradedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
}
