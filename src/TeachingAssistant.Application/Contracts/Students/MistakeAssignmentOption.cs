namespace TeachingAssistant.Application.Contracts;

public record MistakeAssignmentOption(string AssignmentId, string? Title, DateTimeOffset? DueAt, int Count);
