namespace TeachingAssistant.Application.Contracts;

public record MistakeAssignmentOption
{
    public required string AssignmentId { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? DueAt { get; init; }
    public required int Count { get; init; }
}
