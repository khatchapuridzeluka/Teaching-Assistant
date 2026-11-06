namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// The caller's input breaks a rule of the use case (e.g. a chat that does not end with the teacher's message).
/// The API answers 400 with the message. Not used for programming errors, which stay ordinary exceptions.
/// </summary>
public class InvalidRequestException(string message) : Exception(message);
