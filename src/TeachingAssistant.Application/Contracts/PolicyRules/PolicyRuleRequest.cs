using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record PolicyRuleRequest
{
    public required string Title { get; init; }
    public required PolicyEffect Effect { get; init; }
    public double? Points { get; init; }
    public string? PointsPer { get; init; }
    public required string Covers { get; init; }
    public required string Detail { get; init; }
}
