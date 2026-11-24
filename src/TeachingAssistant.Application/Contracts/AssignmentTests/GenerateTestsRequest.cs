using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record GenerateTestsRequest
{
    /// <summary>Console tests (stdin → stdout) or class-design tests against a neutral interface.</summary>
    public required TestStyle Style { get; init; }

    /// <summary>The kinds of cases to write; at least one.</summary>
    public required IReadOnlyList<TestCategory> Categories { get; init; }

    public required TestAmount Amount { get; init; }

    /// <summary>What the teacher wants checked in particular, in their own words.</summary>
    public string? Focus { get; init; }

    /// <summary>What the tests must not check, such as the exact wording of printed messages.</summary>
    public string? DontTest { get; init; }

    /// <summary>The teacher's reference solution as .java files; empty when there is none.</summary>
    public required IReadOnlyList<ReferenceFile> ReferenceFiles { get; init; }
}
