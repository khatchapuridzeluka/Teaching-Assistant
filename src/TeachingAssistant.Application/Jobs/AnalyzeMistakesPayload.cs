namespace TeachingAssistant.Application.Jobs;

public record AnalyzeMistakesPayload
{
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
}
