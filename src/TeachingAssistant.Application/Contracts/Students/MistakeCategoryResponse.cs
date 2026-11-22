using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record MistakeCategoryResponse
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required double Percent { get; init; }
    public required int Assignments { get; init; }
    public required bool Repeated { get; init; }
    public required double PointsLost { get; init; }
    public string? FirstAssignmentTitle { get; init; }
    public string? LastAssignmentTitle { get; init; }
}
