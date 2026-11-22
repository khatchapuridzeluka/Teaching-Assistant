using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>What the compile check found: its status and the compiler's messages (or why it did not run).</summary>
public record BuildCheckOutcome
{
    /// <summary>Whether it compiled, failed, timed out, had nothing to compile or could not run.</summary>
    public required BuildStatus Status { get; init; }

    /// <summary>The compiler's messages, cut to a few thousand characters; for Skipped and Unavailable, the reason.</summary>
    public required string Output { get; init; }

    /// <summary>How many programs (compile units) were compiled.</summary>
    public int Programs { get; init; }

    /// <summary>How many compiler warnings there were.</summary>
    public int Warnings { get; init; }

    /// <summary>How many Checkstyle findings there were; null when Checkstyle did not run.</summary>
    public int? StyleFindings { get; init; }

    /// <summary>Checkstyle's findings, one per line with the submission's paths, or why it could not finish.</summary>
    public string StyleOutput { get; init; } = "";

    /// <summary>How many PMD findings there were; null when PMD did not run.</summary>
    public int? PmdFindings { get; init; }

    /// <summary>PMD's findings, one per line with the submission's paths, or why it could not finish.</summary>
    public string PmdOutput { get; init; } = "";
}
