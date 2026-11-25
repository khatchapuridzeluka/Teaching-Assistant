namespace TeachingAssistant.Application.Jobs;

public record RunTestsPayload
{
    public required Guid TestSetId { get; init; }
    public required Guid VersionId { get; init; }
}
