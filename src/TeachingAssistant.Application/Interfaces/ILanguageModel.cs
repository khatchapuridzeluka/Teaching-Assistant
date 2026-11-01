using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Port to the LLM. The only abstraction in the system that talks to a model;
/// implemented in Infrastructure (e.g. Claude via the Anthropic SDK).
/// </summary>
public interface ILanguageModel
{
    /// <summary>
    /// ID of the configured model, e.g. <c>claude-haiku-5-5</c>.
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// Sends a single prompt and returns the model's text reply.
    /// </summary>
    /// <exception cref="Exceptions.LanguageModelUnavailableException">The model could not be reached or returned an error.</exception>
    Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
