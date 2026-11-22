namespace TeachingAssistant.Application.Models;

public enum SandboxStepOutcome
{
    /// <summary>Exit code 0.</summary>
    Succeeded,

    /// <summary>A non-zero exit code, for example a compile error or an uncaught exception.</summary>
    Failed,

    /// <summary>Stopped because it ran longer than its timeout, for example an endless loop or waiting for more input.</summary>
    TimedOut,

    /// <summary>Not run: an earlier step with StopOnFailure failed, or the run's total time was used up.</summary>
    Skipped,
}
