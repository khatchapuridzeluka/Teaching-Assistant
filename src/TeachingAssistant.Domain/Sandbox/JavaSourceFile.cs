namespace TeachingAssistant.Domain.Sandbox;

/// <summary>A Java source file of a submission: its path inside the submission and its text.</summary>
public record JavaSourceFile
{
    public required string Path { get; init; }
    public required string Text { get; init; }
}
