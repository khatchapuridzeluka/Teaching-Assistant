using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Models;

public record TestRunDetails
{
    public required IReadOnlyList<TestCaseResult> Results { get; init; }
    public IReadOnlyList<string> Unmapped { get; init; } = [];
    public string? Notes { get; init; }
}
