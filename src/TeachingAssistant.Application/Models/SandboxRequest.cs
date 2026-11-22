namespace TeachingAssistant.Application.Models;

/// <summary>
/// What to run in the sandbox: the image, the files to put in its working folder and the steps to run on them.
/// </summary>
public record SandboxRequest
{
    /// <summary>The image to start, for example "ta-java:27".</summary>
    public required string Image { get; init; }

    /// <summary>The files to copy into the working folder before the first step.</summary>
    public required IReadOnlyList<SandboxFile> Files { get; init; }

    /// <summary>The commands to run, in order, in the same container (so a compile step's output is there for the next).</summary>
    public required IReadOnlyList<SandboxStep> Steps { get; init; }

    /// <summary>Limits for this run; null uses the runner's configured defaults.</summary>
    public SandboxLimits? Limits { get; init; }
}

/// <summary>A file for the working folder.</summary>
public record SandboxFile
{
    /// <summary>Relative path with forward slashes, for example "src/shop/Main.java"; never absolute, never "..".</summary>
    public required string Path { get; init; }

    /// <summary>The file's bytes.</summary>
    public required byte[] Content { get; init; }
}

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

/// <summary>Limits for one run; a null value uses the runner's configured default.</summary>
public record SandboxLimits
{
    /// <summary>Memory for the whole container.</summary>
    public int? MemoryMb { get; init; }

    /// <summary>CPU cores for the whole container.</summary>
    public double? Cpus { get; init; }

    /// <summary>How long the whole run may take; steps not started by then are skipped.</summary>
    public TimeSpan? TotalTimeout { get; init; }

    /// <summary>How much of each step's standard output and standard error is kept; the rest is cut off.</summary>
    public int? MaxOutputBytes { get; init; }
}
