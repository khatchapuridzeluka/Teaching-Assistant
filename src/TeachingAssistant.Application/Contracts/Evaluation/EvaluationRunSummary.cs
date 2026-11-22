namespace TeachingAssistant.Application.Contracts;

/// <summary>A run and how far it is: its gradings done, failed (each retried a few times first) and still waiting.</summary>
public record EvaluationRunSummary(
    Guid Id,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Models,
    int Total,
    int Done,
    int Failed,
    int Pending);
