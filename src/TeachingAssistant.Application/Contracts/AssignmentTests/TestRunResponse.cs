using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record TestRunResponse
{
    public required Guid Id { get; init; }
    public required Guid VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }

    /// <summary>The test set version the run used.</summary>
    public required int TestSetVersion { get; init; }

    public required TestRunStatus Status { get; init; }

    /// <summary>One result per case of the test set, in order; empty when the tests could not run.</summary>
    public required IReadOnlyList<TestCaseResult> Results { get; init; }

    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required int NotImplemented { get; init; }

    /// <summary>The adapter that connected the student's classes to the interface.</summary>
    public string? Adapter { get; init; }

    /// <summary>Interface methods the adapter found nothing for in the student's code.</summary>
    public required IReadOnlyList<string> Unmapped { get; init; }

    /// <summary>The adapter writer's notes on mappings a reader might question.</summary>
    public string? Notes { get; init; }

    /// <summary>The compiler's errors or why the run could not finish.</summary>
    public string? Output { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
