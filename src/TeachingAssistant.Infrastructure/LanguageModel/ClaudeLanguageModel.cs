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

/// <summary>
/// <see cref="ILanguageModel"/> backed by Claude through the official Anthropic SDK.
/// Translates SDK exceptions into <see cref="LanguageModelUnavailableException"/>.
/// </summary>
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
                // Simple prompts don't need deep thinking; grading will get its own, stronger settings.
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
        string systemPrompt, IReadOnlyList<PromptPart> parts, CancellationToken cancellationToken = default)
    {
        try
        {
            // The beta endpoint only for refusal fallbacks: if a safety classifier declines (rare for homework, but it
            // would otherwise just stop), the API re-runs the same request on the fallback model within this call.
            var response = await _client.Beta.Messages.Create(new Beta.MessageCreateParams
            {
                Model = _options.GradingModel,
                MaxTokens = _options.GradingMaxTokens,
                // Grading is judgement-heavy; the default effort on this model is medium.
                OutputConfig = new Beta.BetaOutputConfig { Effort = Beta.Effort.High },
                Betas = [AnthropicBeta.ServerSideFallback2026_06_01],
                Fallbacks = new List<Beta.BetaFallbackParam> { new(Model.ClaudeOpus4_8) },
                System = systemPrompt,
                Messages = [new Beta.BetaUserMessageParam { Content = parts.Select(ToBlock).ToList() }],
            }, cancellationToken);

            if (response.StopReason == "refusal")
            {
                throw new LanguageModelUnavailableException("The model declined to grade this submission.");
            }

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<Beta.BetaTextBlock>().Select(t => t.Text)).Trim();
            if (response.StopReason == "max_tokens")
            {
                text += "\n\n_The review was cut off at the length limit (Anthropic:GradingMaxTokens)._";
            }

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

    // Null for exceptions that are not about the Anthropic API; those are bugs and must surface as they are.
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
