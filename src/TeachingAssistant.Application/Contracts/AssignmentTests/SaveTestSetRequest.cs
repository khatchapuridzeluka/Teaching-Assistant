namespace TeachingAssistant.Application.Contracts;

public record SaveTestSetRequest
{
    /// <summary>The requirements in order; they are renumbered R1, R2, … and the cases' references follow.</summary>
    public required IReadOnlyList<TestRequirement> Requirements { get; init; }

    public string? Interface { get; init; }

    /// <summary>The cases in order; they are renumbered T1, T2, ….</summary>
    public required IReadOnlyList<TestCase> Cases { get; init; }
}
