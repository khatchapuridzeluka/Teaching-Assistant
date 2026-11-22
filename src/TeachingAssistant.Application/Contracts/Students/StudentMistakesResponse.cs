using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakesResponse
{
    public required int Total { get; init; }
    public required int Recorded { get; init; }
    public required double PointsLost { get; init; }
    public double? ClassroomPointsLost { get; init; }
    public required int ClassroomGraded { get; init; }
    public required IReadOnlyList<StudentMistakeResponse> Mistakes { get; init; }
    public required IReadOnlyList<MistakeCategoryResponse> Categories { get; init; }
    public required IReadOnlyList<MistakeShare<MistakeSeverity>> Severities { get; init; }
    public required IReadOnlyList<MistakeShare<MistakeLabel>> Labels { get; init; }
    public required IReadOnlyList<MistakeAssignmentResponse> Assignments { get; init; }
    public required IReadOnlyList<MistakeAssignmentOption> AssignmentOptions { get; init; }
}
