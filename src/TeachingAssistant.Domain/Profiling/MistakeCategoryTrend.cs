using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public sealed record MistakeCategoryTrend
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required int Assignments { get; init; }
    public required bool Repeated { get; init; }
    public required double PointsLost { get; init; }
    public required string FirstAssignmentId { get; init; }
    public required string LastAssignmentId { get; init; }
}
