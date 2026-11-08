using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class AgentChatService : IAgentChatService
{
    private const int MaxMessages = 40;
    private const int MaxHistoryLength = 120_000;

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

    private static List<ChatMessage> RecentHistory(IReadOnlyList<ChatMessage> messages)
    {
        var kept = new List<ChatMessage>();
        var length = 0;

        for (var i = messages.Count - 2; i >= 0 && kept.Count < MaxMessages - 1; i--)
        {
            var message = messages[i];
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            length += message.Text.Length;
            if (length > MaxHistoryLength)
            {
                break;
            }

            kept.Add(message);
        }

        kept.Reverse();

        var firstUser = kept.FindIndex(m => m.Role == ChatRole.User);
        kept.RemoveRange(0, firstUser < 0 ? kept.Count : firstUser);
        return kept;
    }
}
