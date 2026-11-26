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
    private readonly ClassroomTools _tools = new(null!, null!, null!, null!, null!, null!); // never called here: only the model calls tools

    [Fact]
    public async Task Reply_PassesTheConversationAndPageToTheModel()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = ChatRole.User, Text = "Hi" }],
            Page = "Assignment 'Assignment 4' in course 'Java II N2'",
        };

        var reply = await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Hello!", reply.Text);
        Assert.Equal("Hi", Assert.Single(_model.Messages!).Text);
        Assert.Contains("Assignment 4", _model.System);
    }

    [Fact]
    public async Task Reply_RejectsAConversationNotEndingWithTheTeacher()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = ChatRole.User, Text = "Hi" }, new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = "Hello",
            }],
        };

        await Assert.ThrowsAsync<InvalidRequestException>(() => service.ReplyAsync(request, TestContext.Current.CancellationToken));
        Assert.Null(_model.Messages); // the model was never called
    }

    [Fact]
    public async Task Reply_KeepsOnlyRecentMessages_StartingWithTheTeacher()
    {
        var service = new AgentChatService(_model, _tools);
        var history = Enumerable.Range(0, 50)
            .Select(i => new ChatMessage { Role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, Text = $"message {i}" })
            .Append(new ChatMessage { Role = ChatRole.User, Text = "latest" })
            .ToList();

        await service.ReplyAsync(new ChatRequest { Messages = history }, TestContext.Current.CancellationToken);

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
        var request = new ChatRequest
        {
            Messages = [
                new ChatMessage { Role = ChatRole.User, Text = "earlier question" },
                new ChatMessage { Role = ChatRole.Assistant, Text = "earlier answer" },
                new ChatMessage { Role = ChatRole.User, Text = text },
            ],
        };

        await Assert.ThrowsAsync<InvalidRequestException>(() => service.ReplyAsync(request, TestContext.Current.CancellationToken));
        Assert.Null(_model.Messages);
    }

    [Fact]
    public async Task Reply_DropsEmptyEarlierMessages_InsteadOfFailing()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest
        {
            Messages = [
                new ChatMessage { Role = ChatRole.User, Text = "first" },
                new ChatMessage { Role = ChatRole.Assistant, Text = "" },
                new ChatMessage { Role = ChatRole.User, Text = "second" },
            ],
        };

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], _model.Messages!.Select(m => m.Text));
    }

    [Fact]
    public async Task Reply_KeepsALongEarlierReply()
    {
        var service = new AgentChatService(_model, _tools);
        var longReply = new string('x', 30_000); // a long model answer, far over the new-message limit
        var request = new ChatRequest
        {
            Messages = [
                new ChatMessage { Role = ChatRole.User, Text = "draft a rubric" },
                new ChatMessage { Role = ChatRole.Assistant, Text = longReply },
                new ChatMessage { Role = ChatRole.User, Text = "shorter please" },
            ],
        };

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
        var request = new ChatRequest
        {
            Messages = [
                new ChatMessage { Role = ChatRole.User, Text = "oldest question" },
                new ChatMessage { Role = ChatRole.Assistant, Text = big },
                new ChatMessage { Role = ChatRole.User, Text = "middle question" },
                new ChatMessage { Role = ChatRole.Assistant, Text = big },
                new ChatMessage { Role = ChatRole.User, Text = "recent question" },
                new ChatMessage { Role = ChatRole.Assistant, Text = big },
                new ChatMessage { Role = ChatRole.User, Text = "latest" },
            ],
        };

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        // "middle question" no longer fits, which would leave a reply first; it is dropped so the chat starts with the teacher.
        Assert.Equal(["recent question", big, "latest"], _model.Messages!.Select(m => m.Text));
    }

    [Fact]
    public async Task Reply_GivesTheModelTheClassroomTools_AndTheOpenPageIds()
    {
        var service = new AgentChatService(_model, _tools);
        var request = new ChatRequest
        {
            Messages = [new ChatMessage { Role = ChatRole.User, Text = "Fetch them" }],
            Page = "Assignment 'A4' in course 'Java'",
            CourseId = "course-1",
            AssignmentId = "assignment-4",
        };

        await service.ReplyAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(_tools, Assert.Single(_model.Tools!));
        Assert.Contains("Assignment 'A4' in course 'Java' (course id course-1, assignment id assignment-4)", _model.System);
    }

    [Fact]
    public async Task Reply_WithoutAnOpenPage_AddsNoPageLine()
    {
        var service = new AgentChatService(_model, _tools);

        await service.ReplyAsync(new ChatRequest { Messages = [new ChatMessage { Role = ChatRole.User, Text = "Hi" }] }, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("currently looking at", _model.System);
    }

    private sealed class RecordingModel : ILanguageModel
    {
        public string? System { get; private set; }
        public IReadOnlyList<ChatMessage>? Messages { get; private set; }
        public IReadOnlyList<object>? Tools { get; private set; }

        public string ModelId => "fake-model";

        public string GradingModelId => "fake-grading-model";


        public string AdapterModelId => "adapter-model";

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
            Messages = history.Select(e => new ChatMessage
            {
                Role = e.Role == AgentMessageRole.User ? ChatRole.User : ChatRole.Assistant,
                Text = e.Text,
            }).ToList();
            Tools = tools;
            return Task.FromResult(new AgentReply
            {
                Model = ModelId,
                Entries = [new AgentEntry { Role = AgentMessageRole.Assistant, Text = "Hello!" }],
            });
        }

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<LanguageModelReply> WriteAdapterAsync(

            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>

            throw new NotSupportedException();


        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            IReadOnlyList<TextPart> system, IReadOnlyList<PromptPart> parts, string outputSchema, string? model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
