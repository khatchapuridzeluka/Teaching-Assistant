namespace TeachingAssistant.Application.Contracts;

public record ChatRequest(IReadOnlyList<ChatMessage> Messages, string? Page, string? CourseId = null, string? AssignmentId = null);
