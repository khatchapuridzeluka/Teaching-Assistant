namespace TeachingAssistant.Application.Exceptions;

/// <summary>
/// The sandbox cannot run at all: Docker is not running, the image is not built, or the container could not be started.
/// Grading goes on without build results when this happens.
/// </summary>
public class SandboxUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
