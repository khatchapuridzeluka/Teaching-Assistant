namespace TeachingAssistant.Application.Jobs;

public record GradeAssignmentPayload
{
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public bool Regrade { get; init; }
}
