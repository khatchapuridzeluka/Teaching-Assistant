namespace TeachingAssistant.Application.Contracts;

public record CommonMistakesResponse
{
    public required Guid AnalysisId { get; init; }
    public required DateTimeOffset AnalyzedAt { get; init; }
    public required string Model { get; init; }
    public required string Summary { get; init; }
    public required string NextLecture { get; init; }
    public required int Students { get; init; }
    public required int Mistakes { get; init; }
    public required int Major { get; init; }
    public required int Minor { get; init; }
    public required int Style { get; init; }
    public required IReadOnlyList<MistakeClusterResponse> Groups { get; init; }
}
