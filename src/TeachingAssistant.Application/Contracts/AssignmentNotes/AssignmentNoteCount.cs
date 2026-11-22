namespace TeachingAssistant.Application.Contracts;

public record AssignmentNoteCount
{
    public required string AssignmentId { get; init; }
    public required int Count { get; init; }
}
