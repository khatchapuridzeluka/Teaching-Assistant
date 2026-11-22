namespace TeachingAssistant.Application.Models;

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
