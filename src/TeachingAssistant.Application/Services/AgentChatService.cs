using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class AgentChatService : IAgentChatService
{
    // Older messages are dropped beyond this, so a long chat cannot grow the prompt (and its cost) without limit.
    private const int MaxMessages = 40;
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
        if (request.Messages.Count == 0 || request.Messages[^1].Role != ChatRole.User)
            throw new InvalidRequestException("The conversation must end with the teacher's message.");
        if (request.Messages.Any(m => string.IsNullOrWhiteSpace(m.Text) || m.Text.Length > MaxMessageLength))
            throw new InvalidRequestException($"Messages must be non-empty and at most {MaxMessageLength} characters.");

        var messages = request.Messages.TakeLast(MaxMessages).ToList();
        // The model expects the conversation to start with the user; drop a leading assistant message left by trimming.
        if (messages[0].Role == ChatRole.Assistant)
            messages.RemoveAt(0);

        var system = string.IsNullOrWhiteSpace(request.Page)
            ? SystemPrompt
            : $"{SystemPrompt}\n\nThe teacher is currently looking at: {request.Page}";

        var reply = await _languageModel.ChatAsync(system, messages, cancellationToken);
        return new ChatResponse(reply.Text);
    }
}
