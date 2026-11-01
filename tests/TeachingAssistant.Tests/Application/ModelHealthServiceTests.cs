using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;

namespace TeachingAssistant.Tests.Application;

public class ModelHealthServiceTests
{
    [Test]
    public async Task CheckAsync_ModelReplies_ReturnsHealthyWithReply()
    {
        var service = new ModelHealthService(new FakeLanguageModel(reply: "ok"), TimeProvider.System);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Healthy, Is.True);
            Assert.That(result.Model, Is.EqualTo(FakeLanguageModel.Id));
            Assert.That(result.Reply, Is.EqualTo("ok"));
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public async Task CheckAsync_ModelUnavailable_ReturnsUnhealthyWithError()
    {
        var failure = new LanguageModelUnavailableException("Authentication failed.");
        var service = new ModelHealthService(new FakeLanguageModel(failure: failure), TimeProvider.System);

        var result = await service.CheckAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Healthy, Is.False);
            Assert.That(result.Model, Is.EqualTo(FakeLanguageModel.Id));
            Assert.That(result.Reply, Is.Null);
            Assert.That(result.Error, Is.EqualTo("Authentication failed."));
        });
    }

    [Test]
    public void CheckAsync_UnexpectedException_IsNotSwallowed()
    {
        var service = new ModelHealthService(
            new FakeLanguageModel(failure: new InvalidOperationException("bug")), TimeProvider.System);

        Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckAsync());
    }

    private sealed class FakeLanguageModel(string? reply = null, Exception? failure = null) : ILanguageModel
    {
        public const string Id = "fake-model";

        public string ModelId => Id;

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(new LanguageModelReply(Id, reply!))
                : Task.FromException<LanguageModelReply>(failure);
    }
}
