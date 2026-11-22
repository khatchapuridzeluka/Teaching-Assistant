namespace TeachingAssistant.Domain.Sandbox;

/// <summary>Files that are compiled together: one program, or one task of a submission that holds several.</summary>
public record JavaCompileUnit
{
    /// <summary>The folder the program's packages start from, relative to the submission; "" for the top.</summary>
    public required string Root { get; init; }

    /// <summary>The paths of its .java files, as in the submission.</summary>
    public required IReadOnlyList<string> Files { get; init; }
}
