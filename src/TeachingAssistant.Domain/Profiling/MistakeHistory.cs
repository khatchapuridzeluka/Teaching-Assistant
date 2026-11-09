using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public enum MistakeLabel
{
    New,

    Repeated,
}

public sealed record LabeledMistake(StudentMistake Mistake, MistakeLabel Label, string? FirstAssignmentId);

public sealed record MistakeCategoryTrend(
    MistakeCategory Category,
    int Count,
    int Assignments,
    bool Repeated,
    double PointsLost,
    string FirstAssignmentId,
    string LastAssignmentId);

public static class MistakeHistory
{
    public static IReadOnlyList<LabeledMistake> Label(
        IEnumerable<StudentMistake> mistakes, Func<string, DateTimeOffset?> assignmentDate)
    {
        var ordered = Order(mistakes, assignmentDate);

        return ordered
            .Select(m =>
            {
                var earlier = ordered
                    .Where(other => other.Category == m.Category && other.AssignmentId != m.AssignmentId)
                    .Where(other => When(other, assignmentDate) < When(m, assignmentDate))
                    .Select(other => other.AssignmentId)
                    .FirstOrDefault();
                return new LabeledMistake(m, earlier is null ? MistakeLabel.New : MistakeLabel.Repeated, earlier);
            })
            .ToList();
    }

    public static IReadOnlyList<MistakeCategoryTrend> Trends(
        IEnumerable<StudentMistake> mistakes, Func<string, DateTimeOffset?> assignmentDate)
    {
        var ordered = Order(mistakes, assignmentDate);

        return ordered
            .GroupBy(m => m.Category)
            .Select(g =>
            {
                var assignments = g.Select(m => m.AssignmentId).Distinct().ToList();
                return new MistakeCategoryTrend(g.Key, g.Count(), assignments.Count, assignments.Count > 1,
                    g.Sum(m => m.Points ?? 0), assignments[0], assignments[^1]);
            })
            .OrderByDescending(t => t.PointsLost)
            .ThenByDescending(t => t.Count)
            .ThenBy(t => t.Category)
            .ToList();
    }

    private static List<StudentMistake> Order(IEnumerable<StudentMistake> mistakes, Func<string, DateTimeOffset?> assignmentDate)
    {
        return mistakes
            .OrderBy(m => When(m, assignmentDate))
            .ThenBy(m => m.AssignmentId, StringComparer.Ordinal)
            .ToList();
    }

    private static DateTimeOffset When(StudentMistake mistake, Func<string, DateTimeOffset?> assignmentDate)
    {
        return assignmentDate(mistake.AssignmentId) ?? mistake.ApprovedAt;
    }
}
