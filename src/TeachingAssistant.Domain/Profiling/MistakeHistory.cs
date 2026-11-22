using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Domain.Profiling;

public enum MistakeLabel
{
    New,

    Repeated,
}

public sealed record LabeledMistake
{
    public required StudentMistake Mistake { get; init; }
    public required MistakeLabel Label { get; init; }
    public string? FirstAssignmentId { get; init; }
}

public sealed record MistakeCategoryTrend
{
    public required MistakeCategory Category { get; init; }
    public required int Count { get; init; }
    public required int Assignments { get; init; }
    public required bool Repeated { get; init; }
    public required double PointsLost { get; init; }
    public required string FirstAssignmentId { get; init; }
    public required string LastAssignmentId { get; init; }
}

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
                return new LabeledMistake
                {
                    Mistake = m,
                    Label = earlier is null ? MistakeLabel.New : MistakeLabel.Repeated,
                    FirstAssignmentId = earlier,
                };
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
                return new MistakeCategoryTrend
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Assignments = assignments.Count,
                    Repeated = assignments.Count > 1,
                    PointsLost = g.Sum(m => m.Points ?? 0),
                    FirstAssignmentId = assignments[0],
                    LastAssignmentId = assignments[^1],
                };
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
