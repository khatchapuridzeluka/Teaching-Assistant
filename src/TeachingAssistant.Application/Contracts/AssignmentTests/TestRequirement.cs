namespace TeachingAssistant.Application.Contracts;

public record TestRequirement
{
    /// <summary>R1, R2, … in the order the instructions state them.</summary>
    public required string Id { get; init; }
    public required string Text { get; init; }

    /// <summary>False for what only a reader can judge, such as clean code; Reason says why.</summary>
    public required bool Testable { get; init; }
    public string? Reason { get; init; }

    /// <summary>How many test cases check this requirement, counted in code.</summary>
    public int TestCount { get; init; }
}
