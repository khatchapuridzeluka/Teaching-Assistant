namespace TeachingAssistant.Infrastructure.Sandbox;

/// <summary>What one docker command did: its exit code and the start of what it printed.</summary>
internal sealed record DockerCommandResult
{
    /// <summary>The exit code; -1 when it was stopped from the host for taking too long.</summary>
    public required int ExitCode { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }

    public required bool OutputTruncated { get; init; }

    /// <summary>True when the host stopped the command because it took too long.</summary>
    public required bool HostTimedOut { get; init; }
}
