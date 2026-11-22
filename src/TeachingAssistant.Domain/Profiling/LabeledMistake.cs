namespace TeachingAssistant.Domain.Profiling;

public sealed record LabeledMistake
{
    public required StudentMistake Mistake { get; init; }
    public required MistakeLabel Label { get; init; }
    public string? FirstAssignmentId { get; init; }
}
