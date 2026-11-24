namespace TeachingAssistant.Application.Jobs;

public record GenerateTestsResult
{
    public required Guid TestSetId { get; init; }
}
