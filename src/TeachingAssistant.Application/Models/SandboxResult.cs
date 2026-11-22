namespace TeachingAssistant.Application.Models;

/// <summary>What happened in one sandbox run: every requested step, in order, including the skipped ones.</summary>
public record SandboxResult
{
    /// <summary>One result per requested step.</summary>
    public required IReadOnlyList<SandboxStepResult> Steps { get; init; }

    /// <summary>How long the run took, container start and removal included.</summary>
    public required TimeSpan Duration { get; init; }
}
