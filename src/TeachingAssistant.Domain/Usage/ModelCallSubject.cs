namespace TeachingAssistant.Domain.Usage;

public sealed record ModelCallSubject
{
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
    public Guid? VersionId { get; init; }
}
