namespace TeachingAssistant.Application.Contracts;

/// <summary>A run and how far it is: its gradings done, failed (each retried a few times first) and still waiting.</summary>
public record EvaluationRunSummary
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<string> Models { get; init; }
    public required int Total { get; init; }
    public required int Done { get; init; }
    public required int Failed { get; init; }
    public required int Pending { get; init; }
}
