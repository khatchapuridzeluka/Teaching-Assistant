namespace TeachingAssistant.Domain.Grading;

/// <summary>
/// The scores of one submission, read from its grade history. The teacher returns the work with a grade, the student
/// fixes it, and the teacher returns it again with a new grade: the first returned grade is the initial score, the
/// latest one the final score. Only returned (assigned) grades count; drafts the student never saw are ignored.
/// </summary>
public static class Grades
{
    /// <summary>The first grade the student saw; null before the work was returned with a grade.</summary>
    public static double? Initial(IEnumerable<GradeEvent> history) =>
        Assigned(history).FirstOrDefault(e => e.PointsEarned is not null)?.PointsEarned;

    /// <summary>
    /// The latest grade the student saw. When the history has no returned grade (older submissions or a source that
    /// keeps no history), the grade the source shows now is used. Null when the work has no grade.
    /// </summary>
    public static double? Final(IEnumerable<GradeEvent> history, double? currentGrade)
    {
        var assigned = Assigned(history).ToList();
        return assigned.Count > 0 ? assigned[^1].PointsEarned : currentGrade;
    }

    private static IEnumerable<GradeEvent> Assigned(IEnumerable<GradeEvent> history) =>
        history.Where(e => e.Kind == GradeEventKind.AssignedGrade).OrderBy(e => e.GradedAt);
}
