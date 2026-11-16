namespace TeachingAssistant.Domain.Grading;

/// <summary>What the compile check found before a grading. Null on a grading means it was graded before the check existed.</summary>
public enum BuildStatus
{
    /// <summary>Every Java program of the submission compiled.</summary>
    Compiled,

    /// <summary>At least one program did not compile; the compiler's messages are stored with it.</summary>
    CompileFailed,

    /// <summary>Compiling took longer than allowed and was stopped.</summary>
    TimedOut,

    /// <summary>Nothing to compile: the submission has no Java source files.</summary>
    Skipped,

    /// <summary>The check could not run (Docker not running, the sandbox image not built); graded from the code alone.</summary>
    Unavailable,
}
