using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentAnalysisResponse(
    Guid Id,
    StudentAnalysisKind Kind,
    string Model,
    string Summary,
    string NextSteps,
    int Assignments,
    int Records,
    DateTimeOffset CreatedAt,
    bool Outdated,
    IReadOnlyList<StrengthGroupResponse> Groups);

public record StrengthGroupResponse(
    string Title,
    string Explanation,
    bool Ungrouped,
    int Count,
    int Assignments,
    IReadOnlyList<MistakeCategory> Categories,
    IReadOnlyList<GroupedStrength> Strengths);

public record GroupedStrength(Guid Id, string? AssignmentTitle, MistakeCategory Category, string Message);
