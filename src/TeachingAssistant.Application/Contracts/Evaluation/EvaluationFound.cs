namespace TeachingAssistant.Application.Contracts;

public record EvaluationFound
{
    /// <summary>Approved reports that count; Reports says how many of them this model has graded.</summary>
    public required int ApprovedReports { get; init; }

    /// <summary>The mistakes you kept in the graded reports.</summary>
    public required int TeacherMistakes { get; init; }

    /// <summary>How many of them the model found again (same file and overlapping lines, or the same category for a whole-submission mistake).</summary>
    public required int Found { get; init; }

    /// <summary>The model's mistakes that repeat one you rejected.</summary>
    public required int RepeatedFalseAlarms { get; init; }

    /// <summary>The model's mistakes that match nothing in your report, so nobody has judged them.</summary>
    public required int Unreviewed { get; init; }

    public required IReadOnlyList<CategoryFound> ByCategory { get; init; }

    public required IReadOnlyList<SeverityFound> BySeverity { get; init; }
}
