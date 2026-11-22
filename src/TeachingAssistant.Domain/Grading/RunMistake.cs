namespace TeachingAssistant.Domain.Grading;

/// <summary>A mistake as a model found it in an evaluation run.</summary>
public sealed record RunMistake
{
    public string? File { get; init; }
    public int? StartLine { get; init; }
    public int? EndLine { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required MistakeCategory Category { get; init; }
    public required string Message { get; init; }
    public double? Points { get; init; }
}
