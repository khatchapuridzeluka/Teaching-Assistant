namespace TeachingAssistant.Application.Contracts;

public record ChatRequest(IReadOnlyList<ChatMessage> Messages, string? Page, string? CourseId = null, string? AssignmentId = null);

public record ChatResponse(string Text);

public record ChatMessage(ChatRole Role, string Text);

public enum ChatRole
{
    User,
    Assistant,
}
