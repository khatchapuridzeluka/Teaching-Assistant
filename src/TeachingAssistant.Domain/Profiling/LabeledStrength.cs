namespace TeachingAssistant.Domain.Profiling;

public sealed record LabeledStrength
{
    public required StudentStrength Strength { get; init; }
    public required StrengthLabel Label { get; init; }
    public string? SinceAssignmentId { get; init; }
}
