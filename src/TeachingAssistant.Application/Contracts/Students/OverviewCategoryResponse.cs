using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record OverviewCategoryResponse
{
    public required MistakeCategory Category { get; init; }
    public required int Strengths { get; init; }
    public required int Mistakes { get; init; }
    public required double PointsLost { get; init; }
}
