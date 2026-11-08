namespace TeachingAssistant.Application.Exceptions;

public class SubmissionSourceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
