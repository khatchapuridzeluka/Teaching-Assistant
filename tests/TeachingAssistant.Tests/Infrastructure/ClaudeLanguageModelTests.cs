using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Infrastructure.LanguageModel;

namespace TeachingAssistant.Tests.Infrastructure;

// The Anthropic API is replaced by a handler that answers with a recorded event stream, so nothing is sent or paid for.
public class ClaudeLanguageModelTests
{
    private const string Schema = """{"type":"object","properties":{"score":{"type":"number"}},"required":["score"],"additionalProperties":false}""";

    [Fact]
    public async Task Grade_JoinsTheStreamedAnswer_SkipsTheThinking_AndAsksForMediumEffort()
    {
        var api = new FakeAnthropic(Stream("end_turn", "{\"score\":", "7.5}"));
        var model = CreateModel(api);

        var reply = await model.GradeAsync([new TextPart { Text = "system" }], [new TextPart { Text = "submission" }], Schema,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("{\"score\":7.5}", reply.Text);
        Assert.Equal("claude-opus-5-5", reply.Model);
        Assert.Equal(120, reply.Usage!.InputTokens);
        Assert.Equal(42, reply.Usage!.OutputTokens);

        using var request = JsonDocument.Parse(api.RequestBody!);
        Assert.True(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(64000, request.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("medium", request.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task Grade_MarksTheCacheOnlyOnTheBlocksTheApplicationMarked()
    {
        var api = new FakeAnthropic(Stream("end_turn", "{\"score\":7}"));
        var model = CreateModel(api);

        await model.GradeAsync(
            [new TextPart { Text = "general rules", CacheAfter = true }, new TextPart { Text = "assignment notes" }],
            [
                new TextPart { Text = "<task>" },
                new FilePart { Name = "task.pdf", MediaType = "application/pdf", Content = [1, 2, 3], CacheAfter = true },
                new TextPart { Text = "student code" },
            ],
            Schema, cancellationToken: TestContext.Current.CancellationToken);

        using var request = JsonDocument.Parse(api.RequestBody!);
        var system = request.RootElement.GetProperty("system").EnumerateArray().ToList();
        Assert.Equal(["general rules", "assignment notes"], system.Select(b => b.GetProperty("text").GetString()));
        Assert.Equal("ephemeral", system[0].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.False(system[1].TryGetProperty("cache_control", out _));

        var content = request.RootElement.GetProperty("messages")[0].GetProperty("content").EnumerateArray().ToList();
        Assert.Equal(["text", "document", "text"], content.Select(b => b.GetProperty("type").GetString()));
        Assert.False(content[0].TryGetProperty("cache_control", out _));
        Assert.Equal("ephemeral", content[1].GetProperty("cache_control").GetProperty("type").GetString());
        Assert.False(content[2].TryGetProperty("cache_control", out _));
    }

    [Fact]
    public async Task Grade_CutOffAtTheLengthLimit_FailsWithAnErrorThatIsNotRetried()
    {
        var model = CreateModel(new FakeAnthropic(Stream("max_tokens", "{\"score\":")));

        var error = await Assert.ThrowsAsync<LanguageModelResponseException>(() => model.GradeAsync(
            [new TextPart { Text = "system" }], [new TextPart { Text = "submission" }], Schema, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("length limit", error.Message);
        Assert.False(JobErrors.IsTemporary(error));
    }

    [Fact]
    public async Task Grade_Refused_FailsWithAnErrorThatIsNotRetried()
    {
        var model = CreateModel(new FakeAnthropic(Stream("refusal")));

        var error = await Assert.ThrowsAsync<LanguageModelResponseException>(() => model.GradeAsync(
            [new TextPart { Text = "system" }], [new TextPart { Text = "submission" }], Schema, cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(JobErrors.IsTemporary(error));
    }

    [Fact]
    public void UnavailableModel_IsStillRetried()
    {
        Assert.True(JobErrors.IsTemporary(new LanguageModelUnavailableException("Rate limited")));
    }

    private static ClaudeLanguageModel CreateModel(FakeAnthropic api)
    {
        var client = new AnthropicClient { ApiKey = "test-key", HttpClient = new HttpClient(api), MaxRetries = 0 };
        return new ClaudeLanguageModel(client, Options.Create(new AnthropicOptions { GradingModel = "claude-opus-5-5" }));
    }

    private static string Stream(string stopReason, params string[] textChunks)
    {
        var events = new StringBuilder();
        void Add(string type, object data)
        {
            events.Append($"event: {type}\ndata: {JsonSerializer.Serialize(data)}\n\n");
        }

        Add("message_start", new
        {
            type = "message_start",
            message = new
            {
                id = "msg_1",
                type = "message",
                role = "assistant",
                model = "claude-opus-5-5",
                content = Array.Empty<object>(),
                stop_reason = (string?)null,
                stop_sequence = (string?)null,
                usage = new { input_tokens = 120, output_tokens = 1 },
            },
        });
        Add("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "thinking", thinking = "", signature = "" } });
        Add("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "thinking_delta", thinking = "{\"score\": 1}" } });
        Add("content_block_stop", new { type = "content_block_stop", index = 0 });
        Add("content_block_start", new { type = "content_block_start", index = 1, content_block = new { type = "text", text = "" } });
        foreach (var chunk in textChunks)
        {
            Add("content_block_delta", new { type = "content_block_delta", index = 1, delta = new { type = "text_delta", text = chunk } });
        }

        Add("content_block_stop", new { type = "content_block_stop", index = 1 });
        Add("message_delta", new
        {
            type = "message_delta",
            delta = new { stop_reason = stopReason, stop_sequence = (string?)null },
            usage = new { output_tokens = 42 },
        });
        Add("message_stop", new { type = "message_stop" });
        return events.ToString();
    }

    private sealed class FakeAnthropic(string stream) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream"),
            };
        }
    }
}
