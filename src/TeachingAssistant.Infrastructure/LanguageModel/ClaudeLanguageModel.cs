using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Messages;
using Beta = Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.LanguageModel;

public class ClaudeLanguageModel : ILanguageModel
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    public ClaudeLanguageModel(AnthropicClient client, IOptions<AnthropicOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public string ModelId => _options.Model;

    public string GradingModelId => _options.GradingModel;

    public async Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new UserMessageParam { Content = prompt }],
            }, cancellationToken);

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
            return new LanguageModelReply { Model = _options.Model, Text = text.Trim(), Usage = ToUsage(response.Usage) };
        }
        catch (Exception ex) when (Translate(ex, _options.Model) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<LanguageModelReply> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.ChatMaxTokens,
                System = systemPrompt,
                Messages = messages.Select(ToParam).ToList(),
            }, cancellationToken);

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
            return new LanguageModelReply { Model = _options.Model, Text = text.Trim(), Usage = ToUsage(response.Usage) };
        }
        catch (Exception ex) when (Translate(ex, _options.Model) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<AgentReply> RunAgentAsync(
        string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = ClaudeAgent.ChatClient(_client, _options.Model, _options.ChatMaxTokens);
            return await ClaudeAgent.RunAsync(chatClient, _options.Model, systemPrompt, history, tools, hooks, cancellationToken);
        }
        catch (Exception ex) when (Translate(ex, _options.Model) is { } translated)
        {
            throw translated;
        }
    }

    public Task<LanguageModelReply> GradeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
        CancellationToken cancellationToken = default)
    {
        return StructuredAsync(systemPrompt, parts, outputSchema, Beta.Effort.Medium, "The model declined to grade this submission.",
            cancellationToken, model);
    }

    public Task<LanguageModelReply> AnalyzeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
    {
        return StructuredAsync(systemPrompt, parts, outputSchema, Beta.Effort.Medium, "The model declined to analyze these mistakes.",
            cancellationToken);
    }

    private async Task<LanguageModelReply> StructuredAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, Beta.Effort effort, string refusal,
        CancellationToken cancellationToken, string? model = null)
    {
        var chosen = string.IsNullOrWhiteSpace(model) ? _options.GradingModel : model;

        // The server-side fallback names an Opus model, which only an Opus request may fall back to; other models (an
        // evaluation run on Sonnet or Haiku) go without it, and a refusal then surfaces as an error.
        var opus = chosen.StartsWith("claude-opus", StringComparison.Ordinal);

        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(outputSchema)
            ?? throw new ArgumentException("The output schema is empty.", nameof(outputSchema));

        try
        {
            var request = opus
                ? new Beta.MessageCreateParams
                {
                    Model = chosen,
                    MaxTokens = _options.GradingMaxTokens,
                    OutputConfig = new Beta.BetaOutputConfig
                    {
                        Effort = effort,
                        Format = new Beta.BetaJsonOutputFormat { Schema = schema },
                    },
                    Betas = [AnthropicBeta.ServerSideFallback2026_06_01],
                    Fallbacks = new List<Beta.BetaFallbackParam> { new(Model.ClaudeOpus4_8) },
                    System = systemPrompt,
                    Messages = [new Beta.BetaUserMessageParam { Content = parts.Select(ToBlock).ToList() }],
                }
                : new Beta.MessageCreateParams
                {
                    Model = chosen,
                    MaxTokens = _options.GradingMaxTokens,
                    OutputConfig = new Beta.BetaOutputConfig
                    {
                        Effort = effort,
                        Format = new Beta.BetaJsonOutputFormat { Schema = schema },
                    },
                    System = systemPrompt,
                    Messages = [new Beta.BetaUserMessageParam { Content = parts.Select(ToBlock).ToList() }],
                };

            var reply = await StreamAsync(request, cancellationToken);

            if (reply.StopReason == "refusal")
            {
                throw new LanguageModelResponseException(refusal);
            }

            if (reply.StopReason == "max_tokens")
            {
                throw new LanguageModelResponseException(
                    "The answer was cut off at the length limit (Anthropic:GradingMaxTokens), so it could not be read. "
                    + "Trying again gives the same result; raise the limit first.");
            }

            return new LanguageModelReply { Model = reply.Model, Text = reply.Text.Trim(), Usage = reply.Usage };
        }
        catch (Exception ex) when (Translate(ex, chosen) is { } translated)
        {
            throw translated;
        }
    }

    private async Task<StreamedReply> StreamAsync(Beta.MessageCreateParams request, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        string model = request.Model;
        string? stopReason = null;
        LanguageModelUsage usage = new() { InputTokens = 0, OutputTokens = 0, CacheReadTokens = 0, CacheWriteTokens = 0 };

        await foreach (var streamEvent in _client.Beta.Messages.CreateStreaming(request, cancellationToken))
        {
            if (streamEvent.TryPickStart(out var start))
            {
                model = start.Message.Model;
                usage = ToUsage(start.Message.Usage);
            }
            else if (streamEvent.TryPickContentBlockDelta(out var blockDelta) && blockDelta.Delta.TryPickText(out var textDelta))
            {
                text.Append(textDelta.Text);
            }
            else if (streamEvent.TryPickDelta(out var messageDelta))
            {
                if (messageDelta.Delta.StopReason is { } reason)
                {
                    stopReason = reason;
                }

                usage = usage with { OutputTokens = messageDelta.Usage.OutputTokens };
            }
        }

        return new StreamedReply { Model = model, Text = text.ToString(), StopReason = stopReason, Usage = usage };
    }

    private sealed record StreamedReply
    {
        public required string Model { get; init; }
        public required string Text { get; init; }
        public string? StopReason { get; init; }
        public required LanguageModelUsage Usage { get; init; }
    }

    private static Beta.BetaContentBlockParam ToBlock(PromptPart part) => part switch
    {
        TextPart text => new Beta.BetaTextBlockParam { Text = text.Text },
        FilePart { MediaType: "application/pdf" } pdf => new Beta.BetaRequestDocumentBlock
        {
            Source = new Beta.BetaBase64PdfSource { Data = Convert.ToBase64String(pdf.Content) },
            Title = pdf.Name,
        },
        FilePart image when FilePart.IsSupported(image.MediaType) => new Beta.BetaImageBlockParam
        {
            Source = new Beta.BetaBase64ImageSource { Data = Convert.ToBase64String(image.Content), MediaType = image.MediaType },
        },
        _ => throw new ArgumentException($"Unsupported prompt part: {part}", nameof(part)),
    };

    private static LanguageModelUsage ToUsage(Usage usage) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        CacheReadTokens = usage.CacheReadInputTokens ?? 0,
        CacheWriteTokens = usage.CacheCreationInputTokens ?? 0,
    };

    private static LanguageModelUsage ToUsage(Beta.BetaUsage usage) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        CacheReadTokens = usage.CacheReadInputTokens ?? 0,
        CacheWriteTokens = usage.CacheCreationInputTokens ?? 0,
    };

    private static MessageParam ToParam(ChatMessage message) => message.Role == ChatRole.User
        ? new UserMessageParam { Content = message.Text }
        : new AssistantMessageParam { Content = message.Text };

    private static LanguageModelUnavailableException? Translate(Exception ex, string model) => ex switch
    {
        AnthropicUnauthorizedException => new("Authentication failed: the Anthropic API key is missing or invalid.", ex),
        AnthropicNotFoundException => new($"Model '{model}' was not found.", ex),
        AnthropicRateLimitException => new("Rate limited by the Anthropic API. Try again shortly.", ex),
        AnthropicApiException => new($"Anthropic API error: {ex.Message}", ex),
        AnthropicIOException => new("Could not reach the Anthropic API (network error).", ex),
        _ => null,
    };
}
