using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Models;

public record GradingTests
{
    public required int TestSetVersion { get; init; }
    public required TestRunStatus Status { get; init; }
    public required IReadOnlyList<TestRequirement> Requirements { get; init; }
    public required IReadOnlyList<TestCase> Cases { get; init; }
    public required IReadOnlyList<TestCaseResult> Results { get; init; }
}
