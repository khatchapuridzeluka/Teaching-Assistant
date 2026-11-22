namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// How a model's fresh grading of a submission compares with the teacher's approved report for it. Scores compare as a
/// share of the maximum. Mistakes are matched to the report's mistakes by place: the same file with overlapping lines (one
/// line of slack), or, for mistakes about the whole submission, the same category. A fresh mistake that matches one the
/// teacher kept was found; one that matches a mistake the teacher rejected repeats a false alarm; one that matches nothing
/// was never reviewed, so it is counted apart rather than judged.
/// </summary>
public sealed record RunComparison
{
    public double? ModelTotal { get; init; }
    public double? TeacherTotal { get; init; }
    public double? MaxScore { get; init; }
    public required int TeacherMistakes { get; init; }
    public required int Found { get; init; }
    public required int RepeatedFalseAlarms { get; init; }
    public required int Unreviewed { get; init; }

    private const int LineSlack = 1;

    /// <summary>The model's total minus the teacher's, in percent of the maximum: positive when the model was more lenient.</summary>
    public double? DifferencePercent =>
        ModelTotal is { } model && TeacherTotal is { } teacher && MaxScore is > 0 ? (model - teacher) / MaxScore.Value * 100 : null;

    /// <summary>The teacher's kept mistakes the model did not find.</summary>
    public int Missed => TeacherMistakes - Found;

    public double? ModelPercent => ModelTotal is { } model && MaxScore is > 0 ? model / MaxScore.Value * 100 : null;

    public double? TeacherPercent => TeacherTotal is { } teacher && MaxScore is > 0 ? teacher / MaxScore.Value * 100 : null;

    /// <summary>
    /// The model's mistakes that match one of the teacher's, as the model wrote them: Kept when it matches a mistake the teacher
    /// kept (with the teacher's points), Rejected when it repeats one the teacher rejected. Unmatched ones are not judged.
    /// </summary>
    public IReadOnlyList<MistakeComparison> Decisions { get; init; } = [];

    /// <summary>Each mistake the teacher kept, with its category and severity, and whether the model found it.</summary>
    public IReadOnlyList<TeacherMistakeResult> TeacherResults { get; init; } = [];

    public static RunComparison Of(EvaluationRunResult result, GradingReport report, double? draftMaxScore)
    {
        if (result.ReportId != report.Id)
        {
            throw new ArgumentException($"Result {result.Id} belongs to report {result.ReportId}, not {report.Id}.", nameof(report));
        }

        var kept = report.Mistakes.Where(m => m.Decision != MistakeDecision.Rejected).ToList();
        var rejected = report.Mistakes.Where(m => m.Decision == MistakeDecision.Rejected).ToList();
        var unmatchedKept = new List<ReportMistake>(kept);
        var unmatchedRejected = new List<ReportMistake>(rejected);
        var found = 0;
        var repeated = 0;
        var unreviewed = 0;
        var decisions = new List<MistakeComparison>();

        foreach (var mistake in result.Mistakes)
        {
            if (Take(unmatchedKept, mistake) is { } keptMatch)
            {
                found++;
                decisions.Add(new MistakeComparison
                {
                    Severity = mistake.Severity,
                    Category = mistake.Category,
                    Decision = MistakeDecision.Kept,
                    AiPoints = mistake.Points,
                    TeacherPoints = keptMatch.Points,
                });
            }
            else if (Take(unmatchedRejected, mistake) is not null)
            {
                repeated++;
                decisions.Add(new MistakeComparison
                {
                    Severity = mistake.Severity,
                    Category = mistake.Category,
                    Decision = MistakeDecision.Rejected,
                    AiPoints = mistake.Points,
                    TeacherPoints = 0,
                });
            }
            else
            {
                unreviewed++;
            }
        }

        return new RunComparison
        {
            ModelTotal = result.Score is { } score ? score + (result.Bonus ?? 0) : null,
            TeacherTotal = report.Score is { } teacher ? teacher + (report.Bonus ?? 0) : null,
            MaxScore = draftMaxScore ?? result.MaxScore,
            TeacherMistakes = kept.Count,
            Found = found,
            RepeatedFalseAlarms = repeated,
            Unreviewed = unreviewed,
            Decisions = decisions,
            TeacherResults = kept.Select(m => new TeacherMistakeResult
            {
                Category = m.Category,
                Severity = m.Severity,
                Found = !unmatchedKept.Contains(m),
            }).ToList(),
        };
    }

    /// <summary>Removes and returns the first of the teacher's mistakes the model's mistake matches.</summary>
    private static ReportMistake? Take(List<ReportMistake> candidates, RunMistake mistake)
    {
        var match = candidates.FirstOrDefault(c => Matches(c, mistake));
        if (match is not null)
        {
            candidates.Remove(match);
        }

        return match;
    }

    internal static bool Matches(ReportMistake teacher, RunMistake model)
    {
        if (teacher.File is null || model.File is null)
        {
            return teacher.File is null && model.File is null && teacher.Category == model.Category;
        }

        if (!string.Equals(Normalize(teacher.File), Normalize(model.File), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (teacher.StartLine is not { } teacherStart || model.StartLine is not { } modelStart)
        {
            return teacher.Category == model.Category;
        }

        var teacherEnd = teacher.EndLine ?? teacherStart;
        var modelEnd = model.EndLine ?? modelStart;
        return modelStart <= teacherEnd + LineSlack && teacherStart <= modelEnd + LineSlack;
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}

/// <summary>One mistake the teacher kept, as the teacher filed it, and whether a model's fresh grading found it.</summary>
public sealed record TeacherMistakeResult
{
    public required MistakeCategory Category { get; init; }
    public required MistakeSeverity Severity { get; init; }
    public required bool Found { get; init; }
}
