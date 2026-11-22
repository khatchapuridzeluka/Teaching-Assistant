namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// How one approved report differs from the AI draft it was made from: the two totals (score plus bonus) and what the
/// teacher did with each of the AI's mistakes and strengths. The mistakes keep the AI's own category, severity and points,
/// so a category the teacher changed still counts against the category the AI chose.
/// </summary>
public sealed record DraftComparison
{
    public double? AiTotal { get; init; }
    public double? TeacherTotal { get; init; }
    public double? MaxScore { get; init; }
    public required IReadOnlyList<MistakeComparison> Mistakes { get; init; }
    public required int StrengthsKept { get; init; }
    public required int StrengthsEdited { get; init; }
    public required int StrengthsRejected { get; init; }

    /// <summary>AI total minus the teacher's total: positive when the AI was more lenient.</summary>
    public double? Difference => AiTotal is { } ai && TeacherTotal is { } teacher ? ai - teacher : null;

    /// <summary>The difference as a share of the maximum score, in percent, so assignments out of 50 and 100 compare.</summary>
    public double? DifferencePercent => Difference is { } difference ? Percent(difference) : null;

    public double? AiPercent => AiTotal is { } ai ? Percent(ai) : null;

    public double? TeacherPercent => TeacherTotal is { } teacher ? Percent(teacher) : null;

    /// <summary>Within the tolerance (in percent of the maximum) of the teacher's total; null when either total is missing.</summary>
    public bool? Agrees(double tolerancePercent)
    {
        return DifferencePercent is { } difference ? Math.Abs(difference) <= tolerancePercent + 1e-9 : null;
    }

    public static DraftComparison Of(AiGrading draft, GradingReport report)
    {
        if (report.GradingId != draft.Id)
        {
            throw new ArgumentException($"Report {report.Id} was made from grading {report.GradingId}, not {draft.Id}.", nameof(report));
        }

        var sources = draft.Mistakes.ToDictionary(m => m.Id);
        var mistakes = report.Mistakes
            .Where(m => m.SourceMistakeId is { } id && sources.ContainsKey(id))
            .OrderBy(m => m.Position)
            .Select(m =>
            {
                var source = sources[m.SourceMistakeId!.Value];
                return new MistakeComparison
                {
                    Severity = source.Severity,
                    Category = source.Category,
                    Decision = m.Decision,
                    AiPoints = source.Points,
                    TeacherPoints = m.Decision == MistakeDecision.Rejected ? 0 : m.Points,
                };
            })
            .ToList();

        var sourceStrengths = draft.Strengths.Select(s => s.Id).ToHashSet();
        var strengths = report.Strengths.Where(s => s.SourceStrengthId is { } id && sourceStrengths.Contains(id)).ToList();

        return new DraftComparison
        {
            AiTotal = Total(draft.Score, draft.Bonus),
            TeacherTotal = Total(report.Score, report.Bonus),
            MaxScore = draft.MaxScore,
            Mistakes = mistakes,
            StrengthsKept = strengths.Count(s => s.Decision == MistakeDecision.Kept),
            StrengthsEdited = strengths.Count(s => s.Decision == MistakeDecision.Edited),
            StrengthsRejected = strengths.Count(s => s.Decision == MistakeDecision.Rejected),
        };
    }

    private double? Percent(double points)
    {
        return MaxScore is > 0 ? points / MaxScore.Value * 100 : null;
    }

    private static double? Total(double? score, double? bonus)
    {
        return score is { } value ? value + (bonus ?? 0) : null;
    }
}

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
