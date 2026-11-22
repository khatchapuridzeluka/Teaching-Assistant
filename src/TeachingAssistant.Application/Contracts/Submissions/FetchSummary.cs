namespace TeachingAssistant.Application.Contracts;

public record FetchSummary
{
    public required int Submissions { get; init; }
    public required int NewSubmissions { get; init; }
    public required int NewVersions { get; init; }
    public required int Unchanged { get; init; }
    public required int NotSubmitted { get; init; }
    public required IReadOnlyList<FetchProblem> Problems { get; init; }
    public int? InstructionsVersion { get; init; }
    public required bool NewInstructions { get; init; }
}
