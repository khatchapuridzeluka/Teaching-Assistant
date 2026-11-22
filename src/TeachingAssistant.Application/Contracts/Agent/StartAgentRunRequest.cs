namespace TeachingAssistant.Application.Contracts;

public record StartAgentRunRequest(
    string Text, Guid? ConversationId = null, string? Page = null, string? CourseId = null, string? AssignmentId = null);
