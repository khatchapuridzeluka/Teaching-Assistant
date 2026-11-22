using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradeChange
{
    public required GradeEventKind Kind { get; init; }
    public double? PointsEarned { get; init; }
    public double? MaxPoints { get; init; }
    public required DateTimeOffset At { get; init; }
}
