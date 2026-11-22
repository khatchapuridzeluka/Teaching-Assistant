using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Services;

internal sealed record ReadReply
{
    public required string Review { get; init; }
    public double? Score { get; init; }
    public double? MaxScore { get; init; }
    public double? Bonus { get; init; }
    public required IReadOnlyList<CodeMistake> Mistakes { get; init; }
    public required IReadOnlyList<CodeStrength> Strengths { get; init; }
}
