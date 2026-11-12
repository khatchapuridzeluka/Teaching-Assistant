using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

public enum GradingDocumentStatus
{
    Draft,

    InReview,

    Approved,

    Discarded,
}

public record GradingDocumentMistake(
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points,
    string? File,
    int? StartLine,
    int? EndLine);

public record GradingDocumentStrength(MistakeCategory Category, string Message, string? File, int? StartLine, int? EndLine);

public record GradingDocument(
    string StudentName,
    string AssignmentTitle,
    DateTimeOffset? DueAt,
    int VersionNumber,
    DateTimeOffset? TurnedInAt,
    GradingDocumentStatus Status,
    double? Score,
    double? MaxScore,
    double? Bonus,
    double? AiScore,
    double? AiBonus,
    string Review,
    IReadOnlyList<GradingDocumentMistake> Mistakes,
    IReadOnlyList<GradingDocumentStrength> Strengths,
    string Model,
    DateTimeOffset GradedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset GeneratedAt);
