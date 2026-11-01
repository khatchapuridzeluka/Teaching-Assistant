namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.ILanguageModel"/> implementations when the model can't be used:
/// missing or invalid API key, unknown model, rate limit, network failure or provider error.
/// Infrastructure translates provider-specific exceptions into this one, so Application never
/// depends on an SDK's exception types.
/// </summary>
public class LanguageModelUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
