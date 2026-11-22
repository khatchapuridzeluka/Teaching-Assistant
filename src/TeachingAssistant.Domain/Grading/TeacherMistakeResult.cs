namespace TeachingAssistant.Domain.Grading;

/// <summary>One mistake the teacher kept, as the teacher filed it, and whether a model's fresh grading found it.</summary>
public sealed record TeacherMistakeResult
{
    public required MistakeCategory Category { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required bool Found { get; init; }
}
