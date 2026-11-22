using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record StrengthGroupResponse
{
    public required string Title { get; init; }
    public required string Explanation { get; init; }
    public required bool Ungrouped { get; init; }
    public required int Count { get; init; }
    public required int Assignments { get; init; }
    public required IReadOnlyList<MistakeCategory> Categories { get; init; }
    public required IReadOnlyList<GroupedStrength> Strengths { get; init; }
}
