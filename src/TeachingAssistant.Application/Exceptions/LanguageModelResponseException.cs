namespace TeachingAssistant.Application.Exceptions;

public class LanguageModelResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
