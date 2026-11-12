using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

public class AgentRunServiceTests
{
    private readonly FakeChats _chats = new();
    private readonly FakeJobs _jobs = new();
    private readonly ScriptedModel _model = new();
    private readonly ClassroomTools _tools = new(null!, null!, null!, null!); // never called here: only the model calls tools
    private readonly StudentTools _studentTools = new(null!, null!, null!, null!, null!);

    private AgentRunService Service()
    {
        return new AgentRunService(_chats, _jobs, _jobs, _model, _tools, _studentTools, new ModelCallContext(), TimeProvider.System);
    }

    private async Task<AgentRunResponse> StartAsync(string text, Guid? conversationId = null)
    {
        var run = await Service().StartAsync(
            new StartAgentRunRequest(text, conversationId, "Assignment 'A4' in course 'Java'", "course-1", "assignment-4"),
            TestContext.Current.CancellationToken);
        return run!;
    }

    [Fact]
    public async Task Start_StoresTheMessageInANewConversation_AndQueuesTheRun()
    {
        var run = await StartAsync("  fetch assignment 4  ");

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        var message = Assert.Single(run.Messages);
        Assert.Equal((AgentMessageRole.User, "fetch assignment 4", 0), (message.Role, message.Text, message.Sequence));
        Assert.Equal(run.ConversationId, Assert.Single(_chats.Conversations).Id);

        var job = Assert.Single(_jobs.Jobs);
        Assert.Equal((JobType.AgentRun, 1, "course-1", "assignment-4"), (job.Type, job.MaxAttempts, job.CourseId, job.AssignmentId));
        Assert.Equal(run.Id, JobPayloads.Read<AgentRunPayload>(job.Payload).RunId);
        Assert.Equal(1, _jobs.Notified);
    }

    [Fact]
    public async Task Start_RejectsAnEmptyMessage_AndAnUnknownConversation()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => StartAsync("   "));

        var unknown = await Service().StartAsync(new StartAgentRunRequest("hi", Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Null(unknown);
        Assert.Empty(_jobs.Jobs);
    }

    [Fact]
    public async Task Start_RejectsANewMessage_WhileThePreviousRunIsUnfinished()
    {
        var first = await StartAsync("fetch assignment 4");

        await Assert.ThrowsAsync<InvalidRequestException>(() => StartAsync("and grade it", first.ConversationId));
    }

    [Fact]
    public async Task Execute_StoresEveryStepInOrder_AndSucceedsWithTheFinalText()
    {
        var started = await StartAsync("what courses do I have?");
        _model.Reply = new AgentReply("fake-model",
        [
            new AgentEntry(AgentMessageRole.Assistant, "", [new AgentToolCall("call-1", "list_courses", "{}")]),
            new AgentEntry(AgentMessageRole.Tool, "- Java II (course id course-1, ACTIVE)", ToolCallId: "call-1", ToolName: "list_courses"),
            new AgentEntry(AgentMessageRole.Assistant, "You have one course: Java II."),
        ]);

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Succeeded, "You have one course: Java II.", 2), (run.Status, run.Reply, run.Steps));
        Assert.Equal(
            [AgentMessageRole.User, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant],
            run.Messages.Select(m => m.Role));
        Assert.Equal([0, 1, 2, 3], run.Messages.Select(m => m.Sequence));
        Assert.Equal(["list_courses"], run.Messages[1].ToolCalls);
        Assert.Equal("list_courses", run.Messages[2].ToolName);

        Assert.Equal("what courses do I have?", Assert.Single(_model.History!).Text);
        Assert.Contains("(course id course-1, assignment id assignment-4)", _model.System);
        Assert.Equal([_tools, _studentTools], _model.Tools!);
    }

    [Fact]
    public async Task Execute_OfAFollowUp_SendsTheEarlierToolStepsBackToTheModel()
    {
        var first = await StartAsync("what courses do I have?");
        _model.Reply = new AgentReply("fake-model",
        [
            new AgentEntry(AgentMessageRole.Assistant, "", [new AgentToolCall("call-1", "list_courses", "{}")]),
            new AgentEntry(AgentMessageRole.Tool, "- Java II", ToolCallId: "call-1", ToolName: "list_courses"),
            new AgentEntry(AgentMessageRole.Assistant, "Java II."),
        ]);
        await Service().ExecuteAsync(first.Id, TestContext.Current.CancellationToken);

        var second = await StartAsync("and its assignments?", first.ConversationId);
        _model.Reply = new AgentReply("fake-model", [new AgentEntry(AgentMessageRole.Assistant, "Assignment 4.")]);
        await Service().ExecuteAsync(second.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [AgentMessageRole.User, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant, AgentMessageRole.User],
            _model.History!.Select(e => e.Role));
        var call = Assert.Single(_model.History![1].ToolCalls!);
        Assert.Equal(("call-1", "list_courses", "{}"), (call.Id, call.Name, call.Arguments));
        Assert.Equal("call-1", _model.History[2].ToolCallId);

        var conversation = (await Service().GetConversationAsync(first.ConversationId, TestContext.Current.CancellationToken))!;
        Assert.Equal(6, conversation.Messages.Count);
        Assert.Null(conversation.ActiveRunId);
    }

    [Fact]
    public async Task Execute_MarksTheRunFailed_WhenTheModelFails()
    {
        var started = await StartAsync("hi");
        _model.Failure = new LanguageModelUnavailableException("Rate limited by the Anthropic API. Try again shortly.");

        await Assert.ThrowsAsync<LanguageModelUnavailableException>(() =>
            Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken));

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Failed, "Rate limited by the Anthropic API. Try again shortly."), (run.Status, run.Error));
    }

    [Fact]
    public async Task Execute_FailsTheRun_WhenTheModelStopsWithoutAnAnswer()
    {
        var started = await StartAsync("hi");
        _model.Reply = new AgentReply("fake-model",
        [
            new AgentEntry(AgentMessageRole.Assistant, "", [new AgentToolCall("call-1", "list_courses", "{}")]),
            new AgentEntry(AgentMessageRole.Tool, "- Java II", ToolCallId: "call-1", ToolName: "list_courses"),
        ]);

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Failed, "The assistant stopped without an answer."), (run.Status, run.Error));
        Assert.Equal(3, run.Messages.Count); // the steps it did take are kept
    }

    [Fact]
    public async Task Execute_DoesNothing_ForAFinishedRun()
    {
        var started = await StartAsync("hi");
        _model.Reply = new AgentReply("fake-model", [new AgentEntry(AgentMessageRole.Assistant, "Hello!")]);
        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, _model.Calls);
    }

    private sealed class ScriptedModel : ILanguageModel
    {
        public AgentReply Reply { get; set; } = new("fake-model", []);

        public Exception? Failure { get; set; }

        public int Calls { get; private set; }

        public string? System { get; private set; }

        public IReadOnlyList<AgentEntry>? History { get; private set; }

        public IReadOnlyList<object>? Tools { get; private set; }

        public string ModelId => "fake-model";

        public string GradingModelId => "fake-grading-model";

        public Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            System = systemPrompt;
            History = history;
            Tools = tools;
            return Failure is null ? Task.FromResult(Reply) : Task.FromException<AgentReply>(Failure);
        }

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeChats : IAgentChatStore
    {
        public List<AgentConversation> Conversations { get; } = [];

        public List<AgentRun> Runs { get; } = [];

        public List<AgentMessage> Messages { get; } = [];

        public Task<AgentConversation?> FindConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Conversations.FirstOrDefault(c => c.Id == conversationId));

        public Task<AgentRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Runs.FirstOrDefault(r => r.Id == runId));

        public Task<AgentRun?> FindUnfinishedRunAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Runs.FirstOrDefault(r => r.ConversationId == conversationId && !r.IsFinished));

        public Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.ConversationId == conversationId).OrderBy(m => m.Sequence).ToList());

        public Task<IReadOnlyList<AgentMessage>> GetRunMessagesAsync(Guid runId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.RunId == runId).OrderBy(m => m.Sequence).ToList());

        public void Add(AgentConversation conversation) => Conversations.Add(conversation);

        public void Add(AgentRun run) => Runs.Add(run);

        public void Add(AgentMessage message) => Messages.Add(message);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeJobs : IJobStore, IJobSignal
    {
        public List<Job> Jobs { get; } = [];

        public int Notified { get; private set; }

        public void Notify() => Notified++;

        public Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default)
        {
            Jobs.Add(job);
            return Task.FromResult(job);
        }

        public Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetRecentAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
            DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(
            IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(
            IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
