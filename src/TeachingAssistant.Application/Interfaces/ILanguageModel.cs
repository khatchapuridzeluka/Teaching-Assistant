using TeachingAssistant.Application.Contracts;
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

    /// <summary>
    /// Continues a conversation and returns the model's whole reply.
    /// </summary>
    /// <param name="systemPrompt">Instructions for the whole conversation.</param>
    /// <param name="messages">The conversation so far, oldest first, ending with the user's new message.</param>
    /// <param name="cancellationToken">Stops the model when the user cancels.</param>
    /// <exception cref="Exceptions.LanguageModelUnavailableException">The model could not be reached or returned an error.</exception>
    Task<LanguageModelReply> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grades work: sends one message made of the parts in order (text and files) to the grading model, which is
    /// stronger and slower than the chat model, and returns its whole reply.
    /// </summary>
    /// <exception cref="Exceptions.LanguageModelUnavailableException">The model could not be reached, returned an error, or declined.</exception>
    Task<LanguageModelReply> GradeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, CancellationToken cancellationToken = default);
}
