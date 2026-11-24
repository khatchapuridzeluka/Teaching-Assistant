using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record TestSetResponse
{
    public required Guid Id { get; init; }
    public required int Version { get; init; }
    public required TestSetStatus Status { get; init; }
    public required TestStyle Style { get; init; }

    /// <summary>The settings the test set was generated with, so the form can start from them again.</summary>
    public required GenerateTestsRequest Request { get; init; }

    public required IReadOnlyList<TestRequirement> Requirements { get; init; }

    /// <summary>The neutral Java interface the class-design tests call; null for console tests.</summary>
    public string? Interface { get; init; }

    public required IReadOnlyList<TestCase> Cases { get; init; }

    /// <summary>The newest approved version of this assignment's tests, the one gradings will use; null when none is approved.</summary>
    public int? ApprovedVersion { get; init; }

    /// <summary>The JUnit code written from the approved cases; null until it is generated.</summary>
    public TestCode? Code { get; init; }

    public required string Model { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
