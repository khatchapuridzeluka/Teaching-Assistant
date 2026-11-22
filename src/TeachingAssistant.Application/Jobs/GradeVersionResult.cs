namespace TeachingAssistant.Application.Jobs;

public record GradeVersionResult
{
    public required Guid GradingId { get; init; }
}
