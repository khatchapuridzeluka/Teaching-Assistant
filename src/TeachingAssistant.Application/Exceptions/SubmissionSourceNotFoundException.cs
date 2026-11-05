namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// The requested course or assignment does not exist in the source, or the teacher cannot see it.
/// </summary>
public class SubmissionSourceNotFoundException(string message, Exception? innerException = null)
    : Exception(message, innerException);
