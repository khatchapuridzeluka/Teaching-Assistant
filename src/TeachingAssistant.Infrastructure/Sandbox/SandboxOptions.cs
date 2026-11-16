namespace TeachingAssistant.Infrastructure.Sandbox;

/// <summary>
/// How the Docker sandbox runs student code. The defaults suit homework-sized Java programs; a request can lower or raise
/// the memory, CPU, total time and output limits for one run.
/// </summary>
public class SandboxOptions
{
    public const string SectionName = "Sandbox";

    /// <summary>The docker command line program; "docker" when it is on the PATH.</summary>
    public string DockerPath { get; set; } = "docker";

    /// <summary>Memory for one container, swap included (no swap on top).</summary>
    public int MemoryMb { get; set; } = 512;

    /// <summary>CPU cores for one container.</summary>
    public double Cpus { get; set; } = 1;

    /// <summary>Most processes and threads in one container, so a fork or thread bomb stops there.</summary>
    public int MaxProcesses { get; set; } = 128;

    /// <summary>Size of the writable /work folder (and of /tmp), which lives in memory and vanishes with the container.</summary>
    public int WorkFolderMb { get; set; } = 64;

    /// <summary>How long one step may run when the request does not say.</summary>
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long one whole run may take; steps not started by then are skipped.</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How much of each step's standard output and of its standard error is kept.</summary>
    public int MaxOutputBytes { get; set; } = 64 * 1024;

    /// <summary>How long Docker itself may take to start a container, copy the files in or remove it.</summary>
    public TimeSpan DockerCommandTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
