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
            return new LanguageModelReply(_options.Model, text.Trim());
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
            return new LanguageModelReply(_options.Model, text.Trim());
        }
        catch (Exception ex) when (Translate(ex, _options.Model) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<LanguageModelReply> GradeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(outputSchema)
            ?? throw new ArgumentException("The output schema is empty.", nameof(outputSchema));

        try
        {
            var response = await _client.Beta.Messages.Create(new Beta.MessageCreateParams
            {
                Model = _options.GradingModel,
                MaxTokens = _options.GradingMaxTokens,
                OutputConfig = new Beta.BetaOutputConfig
                {
                    Effort = Beta.Effort.High,
                    Format = new Beta.BetaJsonOutputFormat { Schema = schema },
                },
                Betas = [AnthropicBeta.ServerSideFallback2026_06_01],
                Fallbacks = new List<Beta.BetaFallbackParam> { new(Model.ClaudeOpus4_8) },
                System = systemPrompt,
                Messages = [new Beta.BetaUserMessageParam { Content = parts.Select(ToBlock).ToList() }],
            }, cancellationToken);

            if (response.StopReason == "refusal")
            {
                throw new LanguageModelUnavailableException("The model declined to grade this submission.");
            }

            if (response.StopReason == "max_tokens")
            {
                throw new LanguageModelUnavailableException(
                    "The review was cut off at the length limit. Raise Anthropic:GradingMaxTokens and grade again.");
            }

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<Beta.BetaTextBlock>().Select(t => t.Text)).Trim();
            return new LanguageModelReply(response.Model, text);
        }
        catch (Exception ex) when (Translate(ex, _options.GradingModel) is { } translated)
        {
            throw translated;
        }
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
