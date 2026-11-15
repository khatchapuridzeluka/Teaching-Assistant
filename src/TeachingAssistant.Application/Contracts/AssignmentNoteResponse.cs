namespace TeachingAssistant.Application.Contracts;

public record AssignmentNoteResponse(Guid Id, string AssignmentId, string Title, string Detail, DateTimeOffset UpdatedAt);

public record AssignmentNoteRequest(string Title, string Detail);

public record AssignmentNoteCount(string AssignmentId, int Count);
