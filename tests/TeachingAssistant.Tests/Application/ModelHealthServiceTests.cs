using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;

namespace TeachingAssistant.Tests.Application;

public class ModelHealthServiceTests
{
    [Fact]
    public async Task CheckAsync_ModelReplies_ReturnsHealthyWithReply()
    {
        var service = new ModelHealthService(new FakeLanguageModel(reply: "ok"), TimeProvider.System);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Healthy);
        Assert.Equal(FakeLanguageModel.Id, result.Model);
        Assert.Equal("ok", result.Reply);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_ModelUnavailable_ReturnsUnhealthyWithError()
    {
        var failure = new LanguageModelUnavailableException("Authentication failed.");
        var service = new ModelHealthService(new FakeLanguageModel(failure: failure), TimeProvider.System);

        var result = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Healthy);
        Assert.Equal(FakeLanguageModel.Id, result.Model);
        Assert.Null(result.Reply);
        Assert.Equal("Authentication failed.", result.Error);
    }

    [Fact]
    public async Task CheckAsync_UnexpectedException_IsNotSwallowed()
    {
        var service = new ModelHealthService(
            new FakeLanguageModel(failure: new InvalidOperationException("bug")), TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FakeLanguageModel(string? reply = null, Exception? failure = null) : ILanguageModel
    {
        public const string Id = "fake-model";

        public string ModelId => Id;

        public string GradingModelId => Id;

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(new LanguageModelReply(Id, reply!))
                : Task.FromException<LanguageModelReply>(failure);

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
