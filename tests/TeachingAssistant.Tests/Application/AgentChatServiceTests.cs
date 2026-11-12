using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Tests.Application;

public class AgentChatServiceTests
{
    private readonly RecordingModel _model = new();
    private readonly ClassroomTools _tools = new(null!, null!, null!, null!); // never called here: only the model calls tools

    [Fact]
    public async Task Reply_PassesTheConversationAndPageToTheModel()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest([new ChatMessage(ChatRole.User, "Hi")], "Assignment 'Assignment 4' in course 'Java II N2'");

        var reply = await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Hello!", reply.Text);
        Assert.Equal("Hi", Assert.Single(_model.Messages!).Text);
        Assert.Contains("Assignment 4", _model.System);
    }

    [Fact]
    public async Task Reply_RejectsAConversationNotEndingWithTheTeacher()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest([new ChatMessage(ChatRole.User, "Hi"), new ChatMessage(ChatRole.Assistant, "Hello")], null);

        await Assert.ThrowsAsync<InvalidRequestException>(() => service.ReplyAsync(request, TestContext.Current.CancellationToken));
        Assert.Null(_model.Messages); // the model was never called
    }

    [Fact]
    public async Task Reply_KeepsOnlyRecentMessages_StartingWithTheTeacher()
    {
        var service = new AgentChatService(_model, _tools);
        var history = Enumerable.Range(0, 50)
            .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"message {i}"))
            .Append(new ChatMessage(ChatRole.User, "latest"))
            .ToList();

        await service.ReplyAsync(new ChatRequest(history, null), TestContext.Current.CancellationToken);

        Assert.True(_model.Messages!.Count <= 40);
        Assert.Equal(ChatRole.User, _model.Messages[0].Role);
        Assert.Equal("latest", _model.Messages[^1].Text);
    }

    [Theory]
    [InlineData(false)] // whitespace only
    [InlineData(true)] // one character over the limit
    public async Task Reply_RejectsAnEmptyOrTooLongNewMessage(bool tooLong)
    {
        var service = new AgentChatService(_model, _tools);
        var text = tooLong ? new string('x', 8_001) : "   ";
        var request = new ChatRequest(
        [
            new ChatMessage(ChatRole.User, "earlier question"),
            new ChatMessage(ChatRole.Assistant, "earlier answer"),
            new ChatMessage(ChatRole.User, text),
        ], null);

        await Assert.ThrowsAsync<InvalidRequestException>(() => service.ReplyAsync(request, TestContext.Current.CancellationToken));
        Assert.Null(_model.Messages);
    }

    [Fact]
    public async Task Reply_DropsEmptyEarlierMessages_InsteadOfFailing()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest(
        [
            new ChatMessage(ChatRole.User, "first"),
            new ChatMessage(ChatRole.Assistant, ""),
            new ChatMessage(ChatRole.User, "second"),
        ], null);

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], _model.Messages!.Select(m => m.Text));
    }

    [Fact]
    public async Task Reply_KeepsALongEarlierReply()
    {
        var service = new AgentChatService(_model, _tools);
        var longReply = new string('x', 30_000); // a long model answer, far over the new-message limit
        var request = new ChatRequest(
        [
            new ChatMessage(ChatRole.User, "draft a rubric"),
            new ChatMessage(ChatRole.Assistant, longReply),
            new ChatMessage(ChatRole.User, "shorter please"),
        ], null);

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(3, _model.Messages!.Count);
        Assert.Equal(longReply, _model.Messages[1].Text);
    }

    [Fact]
    public async Task Reply_DropsTheOldestMessagesBeyondTheLengthBudget()
    {
        var service = new AgentChatService(_model, _tools);
        // Two of these plus "recent question" fill the 120,000-character budget almost exactly.
        var big = new string('x', 59_990);
        var request = new ChatRequest(
        [
            new ChatMessage(ChatRole.User, "oldest question"),
            new ChatMessage(ChatRole.Assistant, big),
            new ChatMessage(ChatRole.User, "middle question"),
            new ChatMessage(ChatRole.Assistant, big),
            new ChatMessage(ChatRole.User, "recent question"),
            new ChatMessage(ChatRole.Assistant, big),
            new ChatMessage(ChatRole.User, "latest"),
        ], null);

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        // "middle question" no longer fits, which would leave a reply first; it is dropped so the chat starts with the teacher.
        Assert.Equal(["recent question", big, "latest"], _model.Messages!.Select(m => m.Text));
    }

    [Fact]
    public async Task Reply_GivesTheModelTheClassroomTools_AndTheOpenPageIds()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest([new ChatMessage(ChatRole.User, "Fetch them")], "Assignment 'A4' in course 'Java'", "course-1", "assignment-4");

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(_tools, Assert.Single(_model.Tools!));
        Assert.Contains("Assignment 'A4' in course 'Java' (course id course-1, assignment id assignment-4)", _model.System);
    }

    [Fact]
    public async Task Reply_WithoutAnOpenPage_AddsNoPageLine()
    {
        var service = new AgentChatService(_model, _tools);

        await service.ReplyAsync(new ChatRequest([new ChatMessage(ChatRole.User, "Hi")], null), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("currently looking at", _model.System);
    }

    private sealed class RecordingModel : ILanguageModel
    {
        public string? System { get; private set; }
        public IReadOnlyList<ChatMessage>? Messages { get; private set; }
        public IReadOnlyList<object>? Tools { get; private set; }

        public string ModelId => "fake-model";

        public string GradingModelId => "fake-grading-model";

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
            CancellationToken cancellationToken = default)
        {
            System = systemPrompt;
            Messages = history.Select(e => new ChatMessage(e.Role == AgentMessageRole.User ? ChatRole.User : ChatRole.Assistant, e.Text)).ToList();
            Tools = tools;
            return Task.FromResult(new AgentReply(ModelId, [new AgentEntry(AgentMessageRole.Assistant, "Hello!")]));
        }

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
