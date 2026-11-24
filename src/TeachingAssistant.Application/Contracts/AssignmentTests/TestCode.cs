using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Contracts;

public record TestCode
{
    /// <summary>The file the neutral interface is compiled from, for example LinkedListHomework.java.</summary>
    public required string InterfaceFile { get; init; }

    /// <summary>The JUnit test class (AssignmentTests) that calls only the interface, through new StudentAdapter().</summary>
    public required string TestClass { get; init; }

    /// <summary>A placeholder StudentAdapter whose methods only throw, so the tests can be compiled before any student's adapter exists.</summary>
    public required string StubAdapter { get; init; }

    /// <summary>Whether the interface, the placeholder adapter and the tests compiled together in the sandbox.</summary>
    public required TestCodeStatus Status { get; init; }

    /// <summary>The compiler's errors, or why the check could not run.</summary>
    public string? CompileOutput { get; init; }

    public required string Model { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
