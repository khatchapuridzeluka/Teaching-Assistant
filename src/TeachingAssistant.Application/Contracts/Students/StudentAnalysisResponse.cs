using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentAnalysisResponse
{
    public required Guid Id { get; init; }
    public required StudentAnalysisKind Kind { get; init; }
    public required string Model { get; init; }
    public required string Summary { get; init; }
    public required string NextSteps { get; init; }
    public required int Assignments { get; init; }
    public required int Records { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool Outdated { get; init; }
    public required IReadOnlyList<StrengthGroupResponse> Groups { get; init; }
}
