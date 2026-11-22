namespace TeachingAssistant.Application.Jobs;

public record FetchStudentWorkPayload
{
    public required string CourseId { get; init; }
    public required string StudentId { get; init; }
    public required Guid ParentId { get; init; }
    public bool Regrade { get; init; }
}
