using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record TestCaseResult
{
    /// <summary>The case this result belongs to (T1, T2, …).</summary>
    public required string CaseId { get; init; }
    public required TestOutcome Outcome { get; init; }

    /// <summary>What went wrong, from the failed assertion or the exception; null when the case passed.</summary>
    public string? Message { get; init; }
}
