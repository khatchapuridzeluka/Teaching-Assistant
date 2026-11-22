namespace TeachingAssistant.Application.Models;

/// <summary>What happened in one sandbox run: every requested step, in order, including the skipped ones.</summary>
public record SandboxResult
{
    /// <summary>One result per requested step.</summary>
    public required IReadOnlyList<SandboxStepResult> Steps { get; init; }

    /// <summary>How long the run took, container start and removal included.</summary>
    public required TimeSpan Duration { get; init; }
}

/// <summary>What one step did.</summary>
public record SandboxStepResult
{
    /// <summary>The step's name from the request.</summary>
    public required string Name { get; init; }

    /// <summary>Whether it succeeded, failed, ran out of time or was skipped.</summary>
    public required SandboxStepOutcome Outcome { get; init; }

    /// <summary>The command's exit code; null when it timed out or was skipped.</summary>
    public int? ExitCode { get; init; }

    /// <summary>What it printed to standard output, cut off at the output limit.</summary>
    public required string Stdout { get; init; }

    /// <summary>What it printed to standard error (compiler errors, exceptions), cut off at the output limit.</summary>
    public required string Stderr { get; init; }

    /// <summary>True when standard output or standard error was cut off.</summary>
    public required bool OutputTruncated { get; init; }

    /// <summary>How long the step ran.</summary>
    public required TimeSpan Duration { get; init; }
}

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
