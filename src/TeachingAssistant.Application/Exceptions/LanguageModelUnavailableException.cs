namespace TeachingAssistant.Application.Exceptions;

public class LanguageModelUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
