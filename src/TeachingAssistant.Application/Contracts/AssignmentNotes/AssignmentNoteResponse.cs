namespace TeachingAssistant.Application.Contracts;

public record AssignmentNoteResponse(Guid Id, string AssignmentId, string Title, string Detail, DateTimeOffset UpdatedAt);
