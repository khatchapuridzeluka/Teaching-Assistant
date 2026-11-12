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
            model, "fake-model", "You are a test.", [new AgentEntry(AgentMessageRole.User, "say hi")], [tools],
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
            new(AgentMessageRole.User, "say hi"),
            new(AgentMessageRole.Assistant, "", [new AgentToolCall("call-1", "echo", """{"word":"hi"}""")]),
            new(AgentMessageRole.Tool, "echoed hi", ToolCallId: "call-1", ToolName: "echo"),
            new(AgentMessageRole.Notice, "The background job finished."),
        ];

        var reply = await ClaudeAgent.RunAsync(
            model, "fake-model", "You are a test.", history, [new EchoTools()], TestContext.Current.CancellationToken);

        Assert.Equal("Done.", reply.Text);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.User], model.LastRequest.Select(m => m.Role));
        Assert.Equal("echo", Assert.Single(model.LastRequest[1].Contents.OfType<FunctionCallContent>()).Name);
        Assert.Equal("call-1", Assert.Single(model.LastRequest[2].Contents.OfType<FunctionResultContent>()).CallId);
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
