namespace TeachingAssistant.Application.Jobs;

public record EvaluationRunPayload
{
    public required Guid RunId { get; init; }
    public string? CourseId { get; init; }
}
