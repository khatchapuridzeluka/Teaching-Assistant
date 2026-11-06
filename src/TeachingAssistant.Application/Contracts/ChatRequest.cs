namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// A chat turn from the teacher: the conversation so far (oldest first, ending with the teacher's new message)
/// and where in the app the teacher is, e.g. "Assignment 'Assignment 4' in course 'Java II N2'". The page
/// description holds no student data.
/// </summary>
public record ChatRequest(IReadOnlyList<ChatMessage> Messages, string? Page);

/// <summary>The assistant's reply to a chat turn (Markdown).</summary>
public record ChatResponse(string Text);

/// <summary>One message of a chat conversation.</summary>
public record ChatMessage(ChatRole Role, string Text);

/// <summary>Who wrote a chat message.</summary>
public enum ChatRole
{
    User,
    Assistant,
}
