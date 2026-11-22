using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingResponse
{
    public required Guid Id { get; init; }
    public required Guid VersionId { get; init; }
    public required GradingStatus Status { get; init; }
    public required string Model { get; init; }
    public required string Review { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public required IReadOnlyList<CodeMistake> Mistakes { get; init; }
    public required int InstructionsVersion { get; init; }
    public required IReadOnlyList<string> SentFiles { get; init; }
    public required IReadOnlyList<string> SkippedFiles { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<CodeStrength> Strengths { get; init; }
    public BuildStatus? BuildStatus { get; init; }
    public string? BuildOutput { get; init; }
    public int? StyleFindings { get; init; }
    public string? StyleOutput { get; init; }
    public int? PmdFindings { get; init; }
    public string? PmdOutput { get; init; }
    public DiscardReason? DiscardReason { get; init; }
    public string? DiscardNote { get; init; }
}
