using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingResponse(
    Guid Id,
    Guid VersionId,
    GradingStatus Status,
    string Model,
    string Review,
    double? Score,
    double? MaxScore,
    double? Bonus,
    IReadOnlyList<CodeMistake> Mistakes,
    int InstructionsVersion,
    IReadOnlyList<string> SentFiles,
    IReadOnlyList<string> SkippedFiles,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CodeStrength> Strengths,
    BuildStatus? BuildStatus = null,
    string? BuildOutput = null,
    int? StyleFindings = null,
    string? StyleOutput = null,
    int? PmdFindings = null,
    string? PmdOutput = null,
    DiscardReason? DiscardReason = null,
    string? DiscardNote = null);
