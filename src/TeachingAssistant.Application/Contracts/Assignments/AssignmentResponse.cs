namespace TeachingAssistant.Application.Contracts;

public record AssignmentResponse
{
    public required string Id { get; init; }
    public required string CourseId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string WorkType { get; init; }
    public double? MaxPoints { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public string? Link { get; init; }
    public required IReadOnlyList<SubmissionAttachment> Materials { get; init; }
}
