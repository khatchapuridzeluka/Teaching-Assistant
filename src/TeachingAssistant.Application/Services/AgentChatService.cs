using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;

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
        Tools take course, assignment and student ids; when the teacher names a course, assignment or student whose id
        you do not have, look it up first (list the courses or assignments, find the student by name) instead of
        guessing an id. If a name matches several students, ask which one is meant before doing anything for them.
        After a tool returns, report what it says plainly, including a failure and its reason.

        Grading costs money: each grading is a paid AI call. Start a grading only when the teacher asks for it, and
        only for the students and assignments they meant. Grade again (regrade) only when the teacher explicitly asks
        to grade again or regrade; when a grading is refused because a draft exists, tell the teacher instead of
        regrading on your own. The teacher reviews and approves every draft.

        Gradings run in the background and take about a minute each. When the teacher only asks you to start them,
        say they have been started, not that they are done. When your next step needs their results (reporting a
        score, comparing, or work that comes after), start everything first, then call wait_for_jobs once with all
        the job ids; your turn ends and you continue when they have finished, with their results. Never present a
        result you have not received. Approving drafts is the teacher's decision: a fresh draft is not approved yet,
        and work that uses only approved reports does not include it until the teacher approves it.

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

        var history = messages
            .Select(m => new AgentEntry(m.Role == ChatRole.User ? AgentMessageRole.User : AgentMessageRole.Assistant, m.Text))
            .ToList();
        var system = SystemPromptFor(request.Page, request.CourseId, request.AssignmentId);

        var reply = await _languageModel.RunAgentAsync(system, history, [_classroomTools], cancellationToken: cancellationToken);
        return new ChatResponse(reply.Text);
    }

    internal static string SystemPromptFor(string? page, string? courseId, string? assignmentId)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(courseId))
        {
            ids.Add($"course id {courseId}");
        }

        if (!string.IsNullOrWhiteSpace(assignmentId))
        {
            ids.Add($"assignment id {assignmentId}");
        }

        if (string.IsNullOrWhiteSpace(page) && ids.Count == 0)
        {
            return SystemPrompt;
        }

        var open = string.IsNullOrWhiteSpace(page) ? "a page" : page;
        return ids.Count == 0
            ? $"{SystemPrompt}\n\nThe teacher is currently looking at: {open}"
            : $"{SystemPrompt}\n\nThe teacher is currently looking at: {open} ({string.Join(", ", ids)})";
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
