using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class AgentRunService : IAgentRunService
{
    private const int MaxMessageLength = 8_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAgentChatStore _chats;
    private readonly IJobStore _jobs;
    private readonly IJobSignal _signal;
    private readonly ILanguageModel _languageModel;
    private readonly ClassroomTools _classroomTools;
    private readonly StudentTools _studentTools;
    private readonly ModelCallContext _callContext;
    private readonly TimeProvider _timeProvider;

    public AgentRunService(
        IAgentChatStore chats,
        IJobStore jobs,
        IJobSignal signal,
        ILanguageModel languageModel,
        ClassroomTools classroomTools,
        StudentTools studentTools,
        ModelCallContext callContext,
        TimeProvider timeProvider)
    {
        _chats = chats;
        _jobs = jobs;
        _signal = signal;
        _languageModel = languageModel;
        _classroomTools = classroomTools;
        _studentTools = studentTools;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<AgentRunResponse?> StartAsync(StartAgentRunRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaxMessageLength)
        {
            throw new InvalidRequestException($"The message must be non-empty and at most {MaxMessageLength} characters.");
        }

        var now = _timeProvider.GetUtcNow();
        AgentConversation? conversation;
        if (request.ConversationId is { } conversationId)
        {
            conversation = await _chats.FindConversationAsync(conversationId, cancellationToken);
            if (conversation is null)
            {
                return null;
            }

            if (await _chats.FindUnfinishedRunAsync(conversationId, cancellationToken) is not null)
            {
                throw new InvalidRequestException("The assistant is still working on the previous message of this conversation.");
            }
        }
        else
        {
            conversation = new AgentConversation(now);
            _chats.Add(conversation);
        }

        var run = new AgentRun(conversation.Id, now, Blank(request.Page), Blank(request.CourseId), Blank(request.AssignmentId));
        var message = conversation.AddUserMessage(run.Id, request.Text, now);
        _chats.Add(run);
        _chats.Add(message);
        await _chats.SaveChangesAsync(cancellationToken);

        var job = new Job(JobType.AgentRun, JobPayloads.Write(new AgentRunPayload(run.Id)), now, dedupKey: $"agent-run:{run.Id}",
            courseId: run.CourseId, assignmentId: run.AssignmentId, maxAttempts: 1);
        await _jobs.AddOrGetActiveAsync(job, cancellationToken);
        _signal.Notify();

        return ToResponse(run, [message]);
    }

    public async Task<AgentRunResponse?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _chats.FindRunAsync(runId, cancellationToken);
        return run is null ? null : ToResponse(run, await _chats.GetRunMessagesAsync(runId, cancellationToken));
    }

    public async Task<AgentConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _chats.FindConversationAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var messages = await _chats.GetMessagesAsync(conversationId, cancellationToken);
        var active = await _chats.FindUnfinishedRunAsync(conversationId, cancellationToken);
        return new AgentConversationResponse(conversation.Id, active?.Id, messages.Select(ToResponse).ToList());
    }

    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _chats.FindRunAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent run {runId} does not exist.");
        if (run.IsFinished || run.Status == AgentRunStatus.Waiting)
        {
            return;
        }

        var conversation = await _chats.FindConversationAsync(run.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent run {runId} has no conversation.");

        if (run.Status == AgentRunStatus.Queued)
        {
            run.Start(_timeProvider.GetUtcNow());
            await _chats.SaveChangesAsync(cancellationToken);
        }

        Describe(run);

        try
        {
            var history = (await _chats.GetMessagesAsync(conversation.Id, cancellationToken)).Select(ToEntry).ToList();
            var system = AgentChatService.SystemPromptFor(run.Page, run.CourseId, run.AssignmentId);
            var reply = await _languageModel.RunAgentAsync(system, history, [_classroomTools, _studentTools], cancellationToken);
            Record(run, conversation, reply);
        }
        catch (OperationCanceledException)
        {
            run.Cancel(_timeProvider.GetUtcNow());
            await _chats.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message, _timeProvider.GetUtcNow());
            await _chats.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await _chats.SaveChangesAsync(CancellationToken.None);
    }

    private void Record(AgentRun run, AgentConversation conversation, AgentReply reply)
    {
        var now = _timeProvider.GetUtcNow();
        var withinLimit = true;

        foreach (var entry in reply.Entries)
        {
            if (entry.Role == AgentMessageRole.Tool)
            {
                _chats.Add(conversation.AddToolResult(run.Id, entry.ToolCallId ?? "", entry.ToolName ?? "", entry.Text, null, now));
                continue;
            }

            withinLimit = withinLimit && run.TryTakeStep();
            var toolCalls = entry.ToolCalls is { Count: > 0 } calls ? JsonSerializer.Serialize(calls, JsonOptions) : null;
            _chats.Add(conversation.AddAssistantMessage(run.Id, entry.Text, toolCalls, now));
        }

        if (!withinLimit)
        {
            run.Fail($"The assistant stopped after {run.MaxSteps} steps without finishing. Ask again with a smaller request.", now);
        }
        else if (reply.Text.Length == 0)
        {
            run.Fail("The assistant stopped without an answer.", now);
        }
        else
        {
            run.Succeed(reply.Text, now);
        }
    }

    private void Describe(AgentRun run)
    {
        if (run is { CourseId: { } courseId, AssignmentId: { } assignmentId })
        {
            _callContext.DescribeAssignment(courseId, assignmentId);
        }
        else if (run.CourseId is { } course)
        {
            _callContext.DescribeCourse(course);
        }
    }

    private static AgentEntry ToEntry(AgentMessage message)
    {
        var calls = message.ToolCalls is null ? null : JsonSerializer.Deserialize<List<AgentToolCall>>(message.ToolCalls, JsonOptions);
        return new AgentEntry(message.Role, message.Text, calls, message.ToolCallId, message.ToolName);
    }

    private static AgentRunResponse ToResponse(AgentRun run, IReadOnlyList<AgentMessage> messages)
    {
        return new AgentRunResponse(run.Id, run.ConversationId, run.Status, run.Reply, run.Error, run.Steps, run.WaitingJobId,
            run.CreatedAt, run.StartedAt, run.FinishedAt, messages.Select(ToResponse).ToList());
    }

    private static AgentMessageResponse ToResponse(AgentMessage message)
    {
        var calls = message.ToolCalls is null
            ? []
            : JsonSerializer.Deserialize<List<AgentToolCall>>(message.ToolCalls, JsonOptions)!.Select(c => c.Name).ToList();
        return new AgentMessageResponse(message.Id, message.RunId, message.Sequence, message.Role, message.Text, calls, message.ToolName,
            message.JobId, message.CreatedAt);
    }

    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
