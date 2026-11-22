using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public sealed record StrengthCategoryTrend
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required int Assignments { get; init; }
    public required bool Consistent { get; init; }
    public required int Mistakes { get; init; }
    public string? ImprovedSinceAssignmentId { get; init; }
    public required string FirstAssignmentId { get; init; }
    public required string LastAssignmentId { get; init; }
}
