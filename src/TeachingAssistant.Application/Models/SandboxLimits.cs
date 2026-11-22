namespace TeachingAssistant.Application.Models;

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
