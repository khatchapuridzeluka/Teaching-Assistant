namespace TeachingAssistant.Application.Jobs;

public record GenerateTestCodePayload
{
    public required Guid TestSetId { get; init; }
}
