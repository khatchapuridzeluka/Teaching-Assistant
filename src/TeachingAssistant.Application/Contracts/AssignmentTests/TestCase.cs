using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record TestCase
{
    /// <summary>T1, T2, ….</summary>
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required TestCategory Category { get; init; }

    /// <summary>The requirements this case checks (R1, R2, …).</summary>
    public required IReadOnlyList<string> RequirementIds { get; init; }

    /// <summary>The input on standard input (console style) or the calls in order, one per line (class-design style).</summary>
    public required string Steps { get; init; }

    /// <summary>The expected output, value or exception; null when it should come from running the reference solution.</summary>
    public string? Expected { get; init; }
}
