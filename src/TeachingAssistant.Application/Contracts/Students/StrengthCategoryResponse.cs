using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record StrengthCategoryResponse
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required double Percent { get; init; }
    public required int Assignments { get; init; }
    public required bool Consistent { get; init; }
    public required int Mistakes { get; init; }
    public string? ImprovedSinceAssignmentTitle { get; init; }
    public string? FirstAssignmentTitle { get; init; }
    public string? LastAssignmentTitle { get; init; }
}
