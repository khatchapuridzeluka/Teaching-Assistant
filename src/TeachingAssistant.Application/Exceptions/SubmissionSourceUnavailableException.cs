namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// The submission source returned an error or could not be reached.
/// </summary>
public class SubmissionSourceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
