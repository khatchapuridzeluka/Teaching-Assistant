using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public enum StrengthLabel
{
    New,

    Consistent,

    Improved,
}

public sealed record LabeledStrength
{
    public required StudentStrength Strength { get; init; }
    public required StrengthLabel Label { get; init; }
    public string? SinceAssignmentId { get; init; }
}

public sealed record StrengthCategoryTrend
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required int Assignments { get; init; }
    public required bool Consistent { get; init; }
    public required int Mistakes { get; init; }
    public string? ImprovedSinceAssignmentId { get; init; }
    public required string FirstAssignmentId { get; init; }
    public required string LastAssignmentId { get; init; }
}

public static class StrengthHistory
{
    public static IReadOnlyList<LabeledStrength> Label(
        IEnumerable<StudentStrength> strengths, IEnumerable<StudentMistake> mistakes, Func<string, DateTimeOffset?> assignmentDate)
    {
        var ordered = Order(strengths, assignmentDate);
        var mistakeList = mistakes.ToList();

        return ordered
            .Select(s =>
            {
                var when = When(s.AssignmentId, s.ApprovedAt, assignmentDate);
                var earlierStrength = ordered
                    .Where(other => other.Category == s.Category && other.AssignmentId != s.AssignmentId)
                    .Where(other => When(other.AssignmentId, other.ApprovedAt, assignmentDate) < when)
                    .Select(other => other.AssignmentId)
                    .FirstOrDefault();
                if (earlierStrength is not null)
                {
                    return new LabeledStrength
                    {
                        Strength = s,
                        Label = StrengthLabel.Consistent,
                        SinceAssignmentId = earlierStrength,
                    };
                }

                var earlierMistake = EarliestMistakeBefore(mistakeList, s.Category, s.AssignmentId, when, assignmentDate);
                return earlierMistake is null
                    ? new LabeledStrength { Strength = s, Label = StrengthLabel.New }
                    : new LabeledStrength { Strength = s, Label = StrengthLabel.Improved, SinceAssignmentId = earlierMistake };
            })
            .ToList();
    }

    public static IReadOnlyList<StrengthCategoryTrend> Trends(
        IEnumerable<StudentStrength> strengths, IEnumerable<StudentMistake> mistakes, Func<string, DateTimeOffset?> assignmentDate)
    {
        var ordered = Order(strengths, assignmentDate);
        var mistakeList = mistakes.ToList();

        return ordered
            .GroupBy(s => s.Category)
            .Select(g =>
            {
                var assignments = g.Select(s => s.AssignmentId).Distinct().ToList();
                var first = g.First();
                var improvedSince = EarliestMistakeBefore(mistakeList, g.Key, first.AssignmentId,
                    When(first.AssignmentId, first.ApprovedAt, assignmentDate), assignmentDate);
                return new StrengthCategoryTrend
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Assignments = assignments.Count,
                    Consistent = assignments.Count > 1,
                    Mistakes = mistakeList.Count(m => m.Category == g.Key),
                    ImprovedSinceAssignmentId = improvedSince,
                    FirstAssignmentId = assignments[0],
                    LastAssignmentId = assignments[^1],
                };
            })
            .OrderByDescending(t => t.Assignments)
            .ThenByDescending(t => t.Count)
            .ThenBy(t => t.Category)
            .ToList();
    }

    private static string? EarliestMistakeBefore(
        List<StudentMistake> mistakes, MistakeCategory category, string assignmentId, DateTimeOffset when,
        Func<string, DateTimeOffset?> assignmentDate)
    {
        return mistakes
            .Where(m => m.Category == category && m.AssignmentId != assignmentId)
            .Where(m => When(m.AssignmentId, m.ApprovedAt, assignmentDate) < when)
            .OrderBy(m => When(m.AssignmentId, m.ApprovedAt, assignmentDate))
            .Select(m => m.AssignmentId)
            .FirstOrDefault();
    }

    private static List<StudentStrength> Order(IEnumerable<StudentStrength> strengths, Func<string, DateTimeOffset?> assignmentDate)
    {
        return strengths
            .OrderBy(s => When(s.AssignmentId, s.ApprovedAt, assignmentDate))
            .ThenBy(s => s.AssignmentId, StringComparer.Ordinal)
            .ToList();
    }

    private static DateTimeOffset When(string assignmentId, DateTimeOffset approvedAt, Func<string, DateTimeOffset?> assignmentDate)
    {
        return assignmentDate(assignmentId) ?? approvedAt;
    }
}
