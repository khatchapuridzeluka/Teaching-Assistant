using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
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
        catch (Exception ex) when (Translate(ex) is { } translated)
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
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            throw translated;
        }
    }

    private static MessageParam ToParam(ChatMessage message) => message.Role == ChatRole.User
        ? new UserMessageParam { Content = message.Text }
        : new AssistantMessageParam { Content = message.Text };

    // Null for exceptions that are not about the Anthropic API; those are bugs and must surface as they are.
    private LanguageModelUnavailableException? Translate(Exception ex) => ex switch
    {
        AnthropicUnauthorizedException => new("Authentication failed: the Anthropic API key is missing or invalid.", ex),
        AnthropicNotFoundException => new($"Model '{_options.Model}' was not found.", ex),
        AnthropicRateLimitException => new("Rate limited by the Anthropic API. Try again shortly.", ex),
        AnthropicApiException => new($"Anthropic API error: {ex.Message}", ex),
        AnthropicIOException => new("Could not reach the Anthropic API (network error).", ex),
        _ => null,
    };
}
