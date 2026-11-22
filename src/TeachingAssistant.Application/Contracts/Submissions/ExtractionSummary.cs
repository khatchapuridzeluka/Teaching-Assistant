namespace TeachingAssistant.Application.Contracts;

public record ExtractionSummary
{
    public required int Versions { get; init; }
    public required int VersionsWithCode { get; init; }
    public required IReadOnlyList<FetchProblem> Problems { get; init; }
}
