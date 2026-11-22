namespace TeachingAssistant.Application.Jobs;

public record AnalyzeMistakesResult
{
    public required Guid AnalysisId { get; init; }
}
