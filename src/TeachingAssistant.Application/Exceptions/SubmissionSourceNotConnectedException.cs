namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// The submission source cannot be used until the teacher connects (or reconnects) the account.
/// </summary>
public class SubmissionSourceNotConnectedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
