namespace TeachingAssistant.Application.Jobs;

public record AnalyzeStudentResult
{
    public required Guid AnalysisId { get; init; }
}
