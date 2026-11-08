namespace TeachingAssistant.Application.Exceptions;

public class SubmissionSourceNotFoundException(string message, Exception? innerException = null)
    : Exception(message, innerException);
