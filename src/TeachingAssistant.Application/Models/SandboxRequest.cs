namespace TeachingAssistant.Application.Models;

/// <summary>
/// What to run in the sandbox: the image, the files to put in its working folder and the steps to run on them.
/// </summary>
/// <param name="Image">The image to start, for example "ta-java:27".</param>
/// <param name="Files">The files to copy into the working folder before the first step.</param>
/// <param name="Steps">The commands to run, in order, in the same container (so a compile step's output is there for the next).</param>
/// <param name="Limits">Limits for this run; null uses the runner's configured defaults.</param>
public record SandboxRequest(
    string Image,
    IReadOnlyList<SandboxFile> Files,
    IReadOnlyList<SandboxStep> Steps,
    SandboxLimits? Limits = null);

/// <summary>A file for the working folder.</summary>
/// <param name="Path">Relative path with forward slashes, for example "src/shop/Main.java"; never absolute, never "..".</param>
/// <param name="Content">The file's bytes.</param>
public record SandboxFile(string Path, byte[] Content);

/// <summary>One command run in the working folder.</summary>
/// <param name="Name">A short name for the results, for example "compile" or "test 2".</param>
/// <param name="Command">The program and its arguments, run directly (not through a shell), for example ["javac", "-d", "out", "@sources.txt"].</param>
/// <param name="Stdin">Text written to the command's standard input, for console tests; null for none.</param>
/// <param name="Timeout">How long the step may run before it is stopped; null uses the runner's default.</param>
/// <param name="StopOnFailure">When the step fails or times out, skip the steps after it (a failed compile leaves nothing to run).</param>
public record SandboxStep(
    string Name,
    IReadOnlyList<string> Command,
    string? Stdin = null,
    TimeSpan? Timeout = null,
    bool StopOnFailure = false);

/// <summary>Limits for one run; a null value uses the runner's configured default.</summary>
/// <param name="MemoryMb">Memory for the whole container.</param>
/// <param name="Cpus">CPU cores for the whole container.</param>
/// <param name="TotalTimeout">How long the whole run may take; steps not started by then are skipped.</param>
/// <param name="MaxOutputBytes">How much of each step's standard output and standard error is kept; the rest is cut off.</param>
public record SandboxLimits(
    int? MemoryMb = null,
    double? Cpus = null,
    TimeSpan? TotalTimeout = null,
    int? MaxOutputBytes = null);
