namespace TeachingAssistant.Application.Models;

public record GradableVersion
{
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
}
