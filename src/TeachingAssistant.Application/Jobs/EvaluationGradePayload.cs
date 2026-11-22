namespace TeachingAssistant.Application.Jobs;

public record EvaluationGradePayload
{
    public required Guid ResultId { get; init; }
}
