namespace TeachingAssistant.Application.Jobs;

public record GradeVersionPayload
{
    public required Guid VersionId { get; init; }
    public bool PartOfGradeAll { get; init; }
}
