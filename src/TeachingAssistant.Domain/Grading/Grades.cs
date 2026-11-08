namespace TeachingAssistant.Domain.Grading;

public static class Grades
{
    public static double? Initial(IEnumerable<GradeEvent> history) =>
        Assigned(history).FirstOrDefault(e => e.PointsEarned is not null)?.PointsEarned;

    public static double? Final(IEnumerable<GradeEvent> history, double? currentGrade)
    {
        var assigned = Assigned(history).ToList();
        return assigned.Count > 0 ? assigned[^1].PointsEarned : currentGrade;
    }

    private static IEnumerable<GradeEvent> Assigned(IEnumerable<GradeEvent> history) =>
        history.Where(e => e.Kind == GradeEventKind.AssignedGrade).OrderBy(e => e.GradedAt);
}
