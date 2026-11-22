using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public enum GradingDocumentStatus
{
    Draft,

    InReview,

    Approved,

    Discarded,
}

public record GradingDocumentMistake
{
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public double? Points { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
}

public record GradingDocumentStrength
{
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
}

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
