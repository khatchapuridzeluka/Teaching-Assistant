using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public enum StrengthLabel
{
    New,

    Consistent,

    Improved,
}

public sealed record LabeledStrength(StudentStrength Strength, StrengthLabel Label, string? SinceAssignmentId);

public sealed record StrengthCategoryTrend(
    MistakeCategory Category,
    int Count,
    int Assignments,
    bool Consistent,
    int Mistakes,
    string? ImprovedSinceAssignmentId,
    string FirstAssignmentId,
    string LastAssignmentId);

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
                    return new LabeledStrength(s, StrengthLabel.Consistent, earlierStrength);
                }

                var earlierMistake = EarliestMistakeBefore(mistakeList, s.Category, s.AssignmentId, when, assignmentDate);
                return earlierMistake is null
                    ? new LabeledStrength(s, StrengthLabel.New, null)
                    : new LabeledStrength(s, StrengthLabel.Improved, earlierMistake);
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
                return new StrengthCategoryTrend(g.Key, g.Count(), assignments.Count, assignments.Count > 1,
                    mistakeList.Count(m => m.Category == g.Key), improvedSince, assignments[0], assignments[^1]);
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
