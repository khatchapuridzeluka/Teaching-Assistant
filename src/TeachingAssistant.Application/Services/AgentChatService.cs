using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Application.Services;

public class AgentChatService : IAgentChatService
{
    private const int MaxMessages = 40;
    private const int MaxHistoryLength = 120_000;

    private const int MaxMessageLength = 8_000;

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
            return PromptLibrary.Agent;
        }

        var open = string.IsNullOrWhiteSpace(page) ? "a page" : page;
        return ids.Count == 0
            ? $"{PromptLibrary.Agent}\n\nThe teacher is currently looking at: {open}"
            : $"{PromptLibrary.Agent}\n\nThe teacher is currently looking at: {open} ({string.Join(", ", ids)})";
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
