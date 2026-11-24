using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Models;

public record TestSetContent
{
    public required IReadOnlyList<TestRequirement> Requirements { get; init; }
    public string? Interface { get; init; }
    public required IReadOnlyList<TestCase> Cases { get; init; }

    public TestCode? Code { get; init; }
}
