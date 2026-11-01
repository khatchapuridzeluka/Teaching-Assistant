using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
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
        catch (AnthropicUnauthorizedException ex)
        {
            throw new LanguageModelUnavailableException("Authentication failed: the Anthropic API key is missing or invalid.", ex);
        }
        catch (AnthropicNotFoundException ex)
        {
            throw new LanguageModelUnavailableException($"Model '{_options.Model}' was not found.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new LanguageModelUnavailableException("Rate limited by the Anthropic API. Try again shortly.", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new LanguageModelUnavailableException($"Anthropic API error: {ex.Message}", ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new LanguageModelUnavailableException("Could not reach the Anthropic API (network error).", ex);
        }
    }
}
