using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Tests.Application;

public class RecordingLanguageModelTests
{
    private readonly FakeModel _model = new();
    private readonly FakeCalls _calls = new();
    private readonly ModelCallContext _context = new();

    private RecordingLanguageModel Recorder() => new(_model, _calls, _context, TimeProvider.System);

    [Fact]
    public async Task Grading_IsRecordedWithTokensModelAndWhatWasGraded()
    {
        var versionId = Guid.NewGuid();
        _context.Describe("course-1", "assignment-1", versionId);

        var reply = await Recorder().GradeAsync("system", [], "{}", TestContext.Current.CancellationToken);

        Assert.Equal("answer", reply.Text);
        var call = Assert.Single(_calls.Calls);
        Assert.Equal((ModelCallPurpose.Grading, "grading-model", "fallback-model", true), (call.Purpose, call.RequestedModel, call.Model, call.Succeeded));
        Assert.Equal(((long?)1200, (long?)300, (long?)900, (long?)50), (call.InputTokens, call.OutputTokens, call.CacheReadTokens, call.CacheWriteTokens));
        Assert.Equal(("course-1", "assignment-1", (Guid?)versionId), (call.CourseId, call.AssignmentId, call.VersionId));
    }

    [Fact]
    public async Task AGradingStartedByGradeAll_IsRecordedUnderItsOwnPurpose()
    {
        _context.MarkPartOfGradeAll();

        await Recorder().GradeAsync("system", [], "{}", TestContext.Current.CancellationToken);

        Assert.Equal(ModelCallPurpose.GradeAll, Assert.Single(_calls.Calls).Purpose);
    }

    [Fact]
    public async Task AFailedCall_IsRecordedWithItsError_AndStillThrows()
    {
        _model.Failure = new LanguageModelUnavailableException("Rate limited by the Anthropic API. Try again shortly.");

        await Assert.ThrowsAsync<LanguageModelUnavailableException>(() =>
            Recorder().ChatAsync("system", [], TestContext.Current.CancellationToken));

        var call = Assert.Single(_calls.Calls);
        Assert.Equal((ModelCallPurpose.Chat, false, "Rate limited by the Anthropic API. Try again shortly."), (call.Purpose, call.Succeeded, call.Error));
        Assert.Null(call.InputTokens);
        Assert.Null(call.CourseId);
    }

    [Fact]
    public async Task AnAgentRun_IsRecordedAsChat()
    {
        await Recorder().RunAgentAsync("system", [], [], TestContext.Current.CancellationToken);

        var call = Assert.Single(_calls.Calls);
        Assert.Equal((ModelCallPurpose.Chat, true), (call.Purpose, call.Succeeded));
    }

    [Fact]
    public async Task AMistakeAnalysis_IsRecordedUnderItsOwnPurpose_WithTheAssignment()
    {
        _context.DescribeAssignment("course-1", "assignment-1");

        await Recorder().AnalyzeAsync("system", [], "{}", TestContext.Current.CancellationToken);

        var call = Assert.Single(_calls.Calls);
        Assert.Equal((ModelCallPurpose.MistakeAnalysis, "assignment-1", (Guid?)null), (call.Purpose, call.AssignmentId, call.VersionId));
    }

    [Fact]
    public async Task HealthCheck_IsRecordedUnderItsOwnPurpose()
    {
        await Recorder().CompleteAsync("ping", TestContext.Current.CancellationToken);

        Assert.Equal((ModelCallPurpose.HealthCheck, "chat-model"), (_calls.Calls[0].Purpose, _calls.Calls[0].RequestedModel));
    }

    private sealed class FakeModel : ILanguageModel
    {
        public Exception? Failure { get; set; }

        public string ModelId => "chat-model";

        public string GradingModelId => "grading-model";

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) => Reply("chat-model");

        public Task<LanguageModelReply> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            Reply("chat-model");

        public async Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools,
            CancellationToken cancellationToken = default)
        {
            var reply = await Reply("chat-model");
            return new AgentReply(reply.Model, [new AgentEntry(AgentMessageRole.Assistant, reply.Text)], reply.Usage);
        }

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            Reply("grading-model");

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            Reply("fallback-model");

        private Task<LanguageModelReply> Reply(string model)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(new LanguageModelReply(model, "answer", new LanguageModelUsage(1200, 300, 900, 50)));
        }
    }

    private sealed class FakeCalls : IModelCallStore
    {
        public List<ModelCall> Calls { get; } = [];

        public Task RecordAsync(ModelCall call, CancellationToken cancellationToken = default)
        {
            Calls.Add(call);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ModelCall>> GetSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModelCall>>(Calls.Where(c => c.CreatedAt >= since).OrderByDescending(c => c.CreatedAt).ToList());
    }
}
