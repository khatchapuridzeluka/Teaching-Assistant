namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// The Classroom grade of a submission: the latest grade the teacher returned, or the current assigned grade when there is no
/// history. Draft grades never count.
/// </summary>
public static class Grades
{
    public static double? Latest(IEnumerable<GradeEvent> history, double? currentGrade)
    {
        var assigned = Assigned(history).ToList();
        return assigned.Count > 0 ? assigned[^1].PointsEarned : currentGrade;
    }

    private static IEnumerable<GradeEvent> Assigned(IEnumerable<GradeEvent> history) =>
        history.Where(e => e.Kind == GradeEventKind.AssignedGrade).OrderBy(e => e.GradedAt);
}
