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
