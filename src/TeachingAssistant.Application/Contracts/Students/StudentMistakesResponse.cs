using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakesResponse(
    int Total,
    int Recorded,
    double PointsLost,
    double? ClassroomPointsLost,
    int ClassroomGraded,
    IReadOnlyList<StudentMistakeResponse> Mistakes,
    IReadOnlyList<MistakeCategoryResponse> Categories,
    IReadOnlyList<MistakeShare<MistakeSeverity>> Severities,
    IReadOnlyList<MistakeShare<MistakeLabel>> Labels,
    IReadOnlyList<MistakeAssignmentResponse> Assignments,
    IReadOnlyList<MistakeAssignmentOption> AssignmentOptions);
