using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;

namespace TeachingAssistant.Tests.Application;

public class AgentChatServiceTests
{
    private readonly RecordingModel _model = new();

    [Fact]
    public async Task Reply_PassesTheConversationAndPageToTheModel()
    {
        var service = new AgentChatService(_model);
        var request = new ChatRequest([new ChatMessage(ChatRole.User, "Hi")], "Assignment 'Assignment 4' in course 'Java II N2'");

        var reply = await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Hello!", reply.Text);
        Assert.Equal("Hi", Assert.Single(_model.Messages!).Text);
        Assert.Contains("Assignment 4", _model.System);
    }

    [Fact]
    public async Task Reply_RejectsAConversationNotEndingWithTheTeacher()
    {
        var service = new AgentChatService(_model);
        var request = new ChatRequest([new ChatMessage(ChatRole.User, "Hi"), new ChatMessage(ChatRole.Assistant, "Hello")], null);

        await Assert.ThrowsAsync<InvalidRequestException>(() => service.ReplyAsync(request, TestContext.Current.CancellationToken));
        Assert.Null(_model.Messages); // the model was never called
    }

    [Fact]
    public async Task Reply_KeepsOnlyRecentMessages_StartingWithTheTeacher()
    {
        var service = new AgentChatService(_model);
        var history = Enumerable.Range(0, 50)
            .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"message {i}"))
            .Append(new ChatMessage(ChatRole.User, "latest"))
            .ToList();

        await service.ReplyAsync(new ChatRequest(history, null), TestContext.Current.CancellationToken);

        Assert.True(_model.Messages!.Count <= 40);
        Assert.Equal(ChatRole.User, _model.Messages[0].Role);
        Assert.Equal("latest", _model.Messages[^1].Text);
    }

    private sealed class RecordingModel : ILanguageModel
    {
        public string? System { get; private set; }
        public IReadOnlyList<ChatMessage>? Messages { get; private set; }

        public string ModelId => "fake-model";

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            System = systemPrompt;
            Messages = messages;
            return Task.FromResult(new LanguageModelReply(ModelId, "Hello!"));
        }
    }
}
