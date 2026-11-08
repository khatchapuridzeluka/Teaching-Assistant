namespace TeachingAssistant.Application.Contracts;

public record ChatRequest(IReadOnlyList<ChatMessage> Messages, string? Page);

public record ChatResponse(string Text);

public record ChatMessage(ChatRole Role, string Text);

public enum ChatRole
{
    User,
    Assistant,
}
