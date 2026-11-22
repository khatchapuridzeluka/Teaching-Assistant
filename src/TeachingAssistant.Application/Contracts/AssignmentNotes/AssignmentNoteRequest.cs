namespace TeachingAssistant.Application.Contracts;

public record AssignmentNoteRequest
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
}
