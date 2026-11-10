using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Tools;

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

        The tool fetches student submissions from Classroom, extracts their code and grades them against the
        teacher's rubric. You reach that data through your tools: use one whenever the teacher's request needs it.
        Tools take course and assignment ids; when the teacher names a course or assignment whose id you do not
        have, list them first instead of guessing an id. After a tool returns, report what it says plainly,
        including a failure and its reason.

        What no tool gives you, you cannot see or do; say so plainly, and never claim to have done something that no
        tool did. You can always help without tools: explaining concepts, drafting rubric criteria or feedback
        wording, reviewing code the teacher pastes, or planning lessons.

        Be concise and practical; the teacher reads your answers between other work. Write feedback and rubric text
        in English unless asked otherwise.
        """;

    private readonly ILanguageModel _languageModel;
    private readonly ClassroomTools _classroomTools;

    public AgentChatService(ILanguageModel languageModel, ClassroomTools classroomTools)
    {
        _languageModel = languageModel;
        _classroomTools = classroomTools;
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

        var reply = await _languageModel.RunAgentAsync(SystemPrompt + OpenPage(request), messages, [_classroomTools], cancellationToken);
        return new ChatResponse(reply.Text);
    }

    private static string OpenPage(ChatRequest request)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.CourseId))
        {
            ids.Add($"course id {request.CourseId}");
        }

        if (!string.IsNullOrWhiteSpace(request.AssignmentId))
        {
            ids.Add($"assignment id {request.AssignmentId}");
        }

        if (string.IsNullOrWhiteSpace(request.Page) && ids.Count == 0)
        {
            return "";
        }

        var page = string.IsNullOrWhiteSpace(request.Page) ? "a page" : request.Page;
        return ids.Count == 0
            ? $"\n\nThe teacher is currently looking at: {page}"
            : $"\n\nThe teacher is currently looking at: {page} ({string.Join(", ", ids)})";
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
