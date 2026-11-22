using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthsResponse
{
    public required int Total { get; init; }
    public required int Recorded { get; init; }
    public required int RecordedMistakes { get; init; }
    public required IReadOnlyList<StudentStrengthResponse> Strengths { get; init; }
    public required IReadOnlyList<StrengthCategoryResponse> Categories { get; init; }
    public required IReadOnlyList<MistakeShare<StrengthLabel>> Labels { get; init; }
    public required IReadOnlyList<StrengthAssignmentResponse> Assignments { get; init; }
    public required IReadOnlyList<MistakeAssignmentOption> AssignmentOptions { get; init; }
}
