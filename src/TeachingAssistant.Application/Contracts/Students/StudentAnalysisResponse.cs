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
