using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record StrengthGroupResponse(
    string Title,
    string Explanation,
    bool Ungrouped,
    int Count,
    int Assignments,
    IReadOnlyList<MistakeCategory> Categories,
    IReadOnlyList<GroupedStrength> Strengths);
