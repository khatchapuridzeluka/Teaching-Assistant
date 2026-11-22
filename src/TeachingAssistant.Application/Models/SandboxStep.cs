namespace TeachingAssistant.Application.Models;

/// <summary>One command run in the working folder.</summary>
public record SandboxStep
{
    /// <summary>A short name for the results, for example "compile" or "test 2".</summary>
    public required string Name { get; init; }

    /// <summary>The program and its arguments, run directly (not through a shell), for example ["javac", "-d", "out", "@sources.txt"].</summary>
    public required IReadOnlyList<string> Command { get; init; }

    /// <summary>Text written to the command's standard input, for console tests; null for none.</summary>
    public string? Stdin { get; init; }

    /// <summary>How long the step may run before it is stopped; null uses the runner's default.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>When the step fails or times out, skip the steps after it (a failed compile leaves nothing to run).</summary>
    public bool StopOnFailure { get; init; }
}
