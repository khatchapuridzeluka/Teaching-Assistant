using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class AgentChatService : IAgentChatService
{
    // Older messages are dropped beyond these, so a long chat cannot grow the prompt (and its cost) without limit.
    // The length budget counts characters (roughly 30k tokens), since one model reply alone can be ~30k characters.
    private const int MaxMessages = 40;
    private const int MaxHistoryLength = 120_000;

    // Limit for the teacher's new message only; earlier messages (including long model replies) are trimmed, not rejected.
    private const int MaxMessageLength = 8_000;

    private const string SystemPrompt =
        """
        You are the teaching assistant inside a grading tool used by a programming teacher. The teacher teaches
        Advanced Java (OOP, class design, design patterns, console I/O, later SQL, Maven/Gradle, Spring Boot,
        multithreading) and C++ basics, with about 15 students per group, through Google Classroom.

        The tool fetches student submissions from Classroom, extracts their code, and will grade them against the
        teacher's rubric. You cannot see any of that data yet: you have no access to courses, submissions, code or
        grades in this conversation. If the teacher asks about specific students or submissions, say plainly that you
        cannot see them yet, and help with what you can: explaining concepts, drafting rubric criteria or feedback
        wording, reviewing code the teacher pastes, or planning lessons.

        Be concise and practical; the teacher reads your answers between other work. Write feedback and rubric text
        in English unless asked otherwise. Never claim to have done something you cannot do, such as fetching,
        grading or changing anything.
        """;

    private readonly ILanguageModel _languageModel;

    public AgentChatService(ILanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    public async Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // The new message is the teacher's to fix, so it is validated strictly.
        if (request.Messages.Count == 0 || request.Messages[^1] is not { Role: ChatRole.User } question)
        {
            throw new InvalidRequestException("The conversation must end with the teacher's message.");
        }

        if (string.IsNullOrWhiteSpace(question.Text) || question.Text.Length > MaxMessageLength)
        {
            throw new InvalidRequestException($"The message must be non-empty and at most {MaxMessageLength} characters.");
        }

        var messages = RecentHistory(request.Messages);
        messages.Add(question);

        var system = string.IsNullOrWhiteSpace(request.Page)
            ? SystemPrompt
            : $"{SystemPrompt}\n\nThe teacher is currently looking at: {request.Page}";

        var reply = await _languageModel.ChatAsync(system, messages, cancellationToken);
        return new ChatResponse(reply.Text);
    }

    // The messages before the new one, oldest first, cleaned up instead of rejected: they come back from the browser
    // and are not the teacher's to fix, so one bad message (an empty or very long model reply) must not make every
    // later turn fail. Empty messages are dropped, and only the most recent ones within the limits are kept.
    private static List<ChatMessage> RecentHistory(IReadOnlyList<ChatMessage> messages)
    {
        var kept = new List<ChatMessage>();
        var length = 0;

        // Newest first, so the limits keep the latest context; stop at the first message that does not fit,
        // so the kept history has no gaps.
        for (var i = messages.Count - 2; i >= 0 && kept.Count < MaxMessages - 1; i--)
        {
            var message = messages[i];
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue; // the model rejects empty content
            }

            length += message.Text.Length;
            if (length > MaxHistoryLength)
            {
                break;
            }

            kept.Add(message);
        }

        kept.Reverse();

        // The model expects the conversation to start with the user; drop assistant messages left first by trimming.
        var firstUser = kept.FindIndex(m => m.Role == ChatRole.User);
        kept.RemoveRange(0, firstUser < 0 ? kept.Count : firstUser);
        return kept;
    }
}
