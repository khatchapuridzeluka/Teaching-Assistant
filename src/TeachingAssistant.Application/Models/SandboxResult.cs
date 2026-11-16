namespace TeachingAssistant.Application.Models;

/// <summary>What happened in one sandbox run: every requested step, in order, including the skipped ones.</summary>
/// <param name="Steps">One result per requested step.</param>
/// <param name="Duration">How long the run took, container start and removal included.</param>
public record SandboxResult(IReadOnlyList<SandboxStepResult> Steps, TimeSpan Duration);

/// <summary>What one step did.</summary>
/// <param name="Name">The step's name from the request.</param>
/// <param name="Outcome">Whether it succeeded, failed, ran out of time or was skipped.</param>
/// <param name="ExitCode">The command's exit code; null when it timed out or was skipped.</param>
/// <param name="Stdout">What it printed to standard output, cut off at the output limit.</param>
/// <param name="Stderr">What it printed to standard error (compiler errors, exceptions), cut off at the output limit.</param>
/// <param name="OutputTruncated">True when standard output or standard error was cut off.</param>
/// <param name="Duration">How long the step ran.</param>
public record SandboxStepResult(
    string Name,
    SandboxStepOutcome Outcome,
    int? ExitCode,
    string Stdout,
    string Stderr,
    bool OutputTruncated,
    TimeSpan Duration);

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
