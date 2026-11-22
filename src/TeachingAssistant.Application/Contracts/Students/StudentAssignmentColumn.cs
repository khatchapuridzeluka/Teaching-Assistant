namespace TeachingAssistant.Application.Contracts;

public record StudentAssignmentColumn
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public double? MaxPoints { get; init; }
    public DateTimeOffset? DueAt { get; init; }
}
