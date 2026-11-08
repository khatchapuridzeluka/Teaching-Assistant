namespace TeachingAssistant.Application.Exceptions;

public class SubmissionSourceNotConnectedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
