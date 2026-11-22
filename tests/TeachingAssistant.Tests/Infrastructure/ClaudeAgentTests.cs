using System.ComponentModel;
using Microsoft.Extensions.AI;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Infrastructure.LanguageModel;

namespace TeachingAssistant.Tests.Infrastructure;

public class ClaudeAgentTests
{
    [Fact]
    public async Task Run_CallsTheMarkedTool_AndReturnsEveryStepInOrder()
    {
        var model = new ScriptedModel(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "echo", new Dictionary<string, object?> { ["word"] = "hi" })]),
            new ChatMessage(ChatRole.Assistant, "The tool said hi."));
        var tools = new EchoTools();

        var reply = await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", [new AgentEntry { Role = AgentMessageRole.User, Text = "say hi" }], [tools], null,
            TestContext.Current.CancellationToken);

        Assert.Equal(["hi"], tools.Words);
        Assert.Equal(2, model.Calls);
        Assert.Equal(["echo"], model.OfferedTools);
        Assert.Equal("The tool said hi.", reply.Text);
        Assert.Equal([AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant], reply.Entries.Select(e => e.Role));

        var call = Assert.Single(reply.Entries[0].ToolCalls!);
        Assert.Equal(("call-1", "echo", """{"word":"hi"}"""), (call.Id, call.Name, call.Arguments));
        Assert.Equal(("call-1", "echo", "echoed hi"), (reply.Entries[1].ToolCallId, reply.Entries[1].ToolName, reply.Entries[1].Text));
    }

    [Fact]
    public async Task Run_SendsEarlierToolStepsBackToTheModel()
    {
        var model = new ScriptedModel(new ChatMessage(ChatRole.Assistant, "Done."));
        AgentEntry[] history =
        [
            new() { Role = AgentMessageRole.User, Text = "say hi" },
            new()
            {
                Role = AgentMessageRole.Assistant,
                Text = "",
                ToolCalls = [new AgentToolCall
                {
                    Id = "call-1",
                    Name = "echo",
                    Arguments = """{"word":"hi"}""",
                }],
            },
            new() { Role = AgentMessageRole.Tool, Text = "echoed hi", ToolCallId = "call-1", ToolName = "echo" },
            new() { Role = AgentMessageRole.Notice, Text = "The background job finished." },
        ];

        var reply = await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", history, [new EchoTools()], null, TestContext.Current.CancellationToken);

        Assert.Equal("Done.", reply.Text);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.User], model.LastRequest.Select(m => m.Role));
        Assert.Equal("echo", Assert.Single(model.LastRequest[1].Contents.OfType<FunctionCallContent>()).Name);
        Assert.Equal("call-1", Assert.Single(model.LastRequest[2].Contents.OfType<FunctionResultContent>()).CallId);
    }

    [Fact]
    public async Task Run_ReportsEachStepWhenItHappens()
    {
        var model = new ScriptedModel(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "echo", new Dictionary<string, object?> { ["word"] = "hi" })]),
            new ChatMessage(ChatRole.Assistant, "Done."));
        var steps = new List<string>();
        var hooks = new AgentRunHooks
        {
            OnStep = entry =>
            {
                steps.Add($"{entry.Role}:{entry.ToolName ?? entry.ToolCalls?.FirstOrDefault()?.Name ?? entry.Text}:{model.Calls}");
                return Task.CompletedTask;
            },
            ShouldStop = () => false,
        };

        await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", [new AgentEntry { Role = AgentMessageRole.User, Text = "say hi" }], [new EchoTools()], hooks,
            TestContext.Current.CancellationToken);

        Assert.Equal(["Assistant:echo:1", "Tool:echo:1", "Assistant:Done.:2"], steps); // the tool result is reported before the second call
    }

    [Fact]
    public async Task Run_EndsTheLoop_WhenATool_AsksToStop()
    {
        var model = new ScriptedModel(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "echo", new Dictionary<string, object?> { ["word"] = "wait" })]),
            new ChatMessage(ChatRole.Assistant, "Should not be asked for."));
        var tools = new EchoTools();
        var hooks = new AgentRunHooks { OnStep = _ => Task.CompletedTask, ShouldStop = () => tools.Words.Contains("wait") };

        var reply = await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", [new AgentEntry { Role = AgentMessageRole.User, Text = "wait" }], [tools], hooks,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, model.Calls);
        Assert.Equal("", reply.Text);
        Assert.Equal(AgentMessageRole.Tool, reply.Entries[^1].Role);
    }

    [Fact]
    public async Task ACallWithAMissingArgument_IsAnsweredWithTheReason_AndStoredBeforeTheNextStep()
    {
        var model = new ScriptedModel(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "echo", new Dictionary<string, object?> { ["wrd"] = "hi" })]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-2", "echo", new Dictionary<string, object?> { ["word"] = "hi" })]),
            new ChatMessage(ChatRole.Assistant, "Done."));
        var steps = new List<AgentEntry>();
        var hooks = new AgentRunHooks
        {
            OnStep = entry =>
            {
                steps.Add(entry);
                return Task.CompletedTask;
            },
            ShouldStop = () => false,
        };

        await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", [new AgentEntry { Role = AgentMessageRole.User, Text = "say hi" }], [new EchoTools()], hooks,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant],
            steps.Select(s => s.Role));
        Assert.Equal(("call-1", "echo"), (steps[1].ToolCallId, steps[1].ToolName));
        Assert.StartsWith("Failed: the tool could not run:", steps[1].Text);
        Assert.Equal("echoed hi", steps[3].Text);
    }

    [Fact]
    public async Task ACallWhoseArgumentsCouldNotBeRead_IsStoredWithTheFrameworksAnswer_BeforeTheNextStep()
    {
        var broken = new FunctionCallContent("call-1", "echo", new Dictionary<string, object?>())
        {
            Exception = new System.Text.Json.JsonException("'<' is an invalid start of a value."),
        };
        var model = new ScriptedModel(
            new ChatMessage(ChatRole.Assistant, [broken]),
            new ChatMessage(ChatRole.Assistant, "Done."));
        var tools = new EchoTools();
        var steps = new List<AgentEntry>();
        var hooks = new AgentRunHooks
        {
            OnStep = entry =>
            {
                steps.Add(entry);
                return Task.CompletedTask;
            },
            ShouldStop = () => false,
        };

        await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", [new AgentEntry { Role = AgentMessageRole.User, Text = "say hi" }], [tools], hooks,
            TestContext.Current.CancellationToken);

        Assert.Empty(tools.Words);
        Assert.Equal([AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant], steps.Select(s => s.Role));
        Assert.Equal(("call-1", "echo"), (steps[1].ToolCallId, steps[1].ToolName));
        Assert.NotEqual("", steps[1].Text);
    }

    [Fact]
    public async Task AStoredCallWithoutItsResult_GetsAPlaceholderResult_SoTheModelAcceptsTheHistory()
    {
        var model = new ScriptedModel(new ChatMessage(ChatRole.Assistant, "Done."));
        var steps = new List<AgentEntry>();
        var hooks = new AgentRunHooks
        {
            OnStep = entry =>
            {
                steps.Add(entry);
                return Task.CompletedTask;
            },
            ShouldStop = () => false,
        };
        AgentEntry[] history =
        [
            new() { Role = AgentMessageRole.User, Text = "grade it" },
            new()
            {
                Role = AgentMessageRole.Assistant,
                Text = "",
                ToolCalls = [new AgentToolCall { Id = "lost", Name = "echo", Arguments = """{"word":"x"}""" }],
            },
            new()
            {
                Role = AgentMessageRole.Assistant,
                Text = "Again.",
                ToolCalls = [new AgentToolCall
                {
                    Id = "call-2",
                    Name = "echo",
                    Arguments = """{"word":"hi"}""",
                }],
            },
            new() { Role = AgentMessageRole.Tool, Text = "echoed hi", ToolCallId = "call-2", ToolName = "echo" },
            new() { Role = AgentMessageRole.Notice, Text = "The grading finished." },
        ];

        await ClaudeAgent.RunAsync(model, "fake-model", "You are a test.", history, [new EchoTools()], hooks,
            TestContext.Current.CancellationToken);

        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant, ChatRole.Tool, ChatRole.User],
            model.LastRequest.Select(m => m.Role));
        Assert.Equal("lost", Assert.Single(model.LastRequest[2].Contents.OfType<FunctionResultContent>()).CallId);
        Assert.Equal([AgentMessageRole.Assistant], steps.Select(s => s.Role)); // the placeholder and the old result are not stored again
    }

    private sealed class EchoTools
    {
        public List<string> Words { get; } = [];

        [AgentTool("echo")]
        [Description("Echoes a word.")]
        public Task<string> EchoAsync([Description("The word")] string word, CancellationToken cancellationToken)
        {
            Words.Add(word);
            return Task.FromResult($"echoed {word}");
        }

        public string NotATool()
        {
            return "ignored";
        }
    }

    private sealed class ScriptedModel(params ChatMessage[] replies) : IChatClient
    {
        public int Calls { get; private set; }

        public List<string> OfferedTools { get; private set; } = [];

        public List<ChatMessage> LastRequest { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastRequest = messages.ToList();
            OfferedTools = (options?.Tools ?? []).Select(t => t.Name).ToList();
            return Task.FromResult(new ChatResponse(replies[Calls++]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
