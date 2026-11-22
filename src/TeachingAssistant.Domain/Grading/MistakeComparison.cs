namespace TeachingAssistant.Domain.Grading;

/// <summary>One of the AI's mistakes as the AI wrote it (severity, category, points) and what the teacher decided.</summary>
public sealed record MistakeComparison
{
    public required MistakeSeverity Severity { get; init; }

    public required MistakeCategory Category { get; init; }

    public required MistakeDecision Decision { get; init; }

    public double? AiPoints { get; init; }

    /// <summary>The points the teacher kept for it; 0 when rejected.</summary>
    public double? TeacherPoints { get; init; }

    /// <summary>True when the teacher kept the mistake (as it was or edited) but changed how many points it costs.</summary>
    public bool PointsChanged => Decision != MistakeDecision.Rejected && (AiPoints ?? 0) != (TeacherPoints ?? 0);
}
