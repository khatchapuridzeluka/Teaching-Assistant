using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthsResponse(
    int Total,
    int Recorded,
    int RecordedMistakes,
    IReadOnlyList<StudentStrengthResponse> Strengths,
    IReadOnlyList<StrengthCategoryResponse> Categories,
    IReadOnlyList<MistakeShare<StrengthLabel>> Labels,
    IReadOnlyList<StrengthAssignmentResponse> Assignments,
    IReadOnlyList<MistakeAssignmentOption> AssignmentOptions);
