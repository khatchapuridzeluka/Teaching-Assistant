using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Domain;

public class GradesTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReturnedTwice_InitialIsTheFirstReturn_FinalIsTheLatest()
    {
        // Returned with 70, the student fixed it, returned again with 95.
        GradeEvent[] history = [Assigned(70, Day1), Assigned(95, Day1.AddDays(5))];

        Assert.Equal(70, Grades.Initial(history));
        Assert.Equal(95, Grades.Final(history, currentGrade: 95));
    }

    [Fact]
    public void ReturnedOnce_InitialAndFinalAreTheSame()
    {
        GradeEvent[] history = [Assigned(88, Day1)];

        Assert.Equal(88, Grades.Initial(history));
        Assert.Equal(88, Grades.Final(history, currentGrade: 88));
    }

    [Fact]
    public void DraftGrades_AreIgnored()
    {
        // Classroom records a draft change when the grade is typed, then the assigned change when it is returned.
        GradeEvent[] history =
        [
            new(GradeEventKind.DraftGrade, 60, 100, Day1),
            Assigned(65, Day1.AddMinutes(1)),
            new(GradeEventKind.DraftGrade, 90, 100, Day1.AddDays(3)), // typed, not returned yet
        ];

        Assert.Equal(65, Grades.Initial(history));
        Assert.Equal(65, Grades.Final(history, currentGrade: 65));
    }

    [Fact]
    public void HistoryOutOfOrder_IsReadByTime()
    {
        GradeEvent[] history = [Assigned(95, Day1.AddDays(5)), Assigned(70, Day1)];

        Assert.Equal(70, Grades.Initial(history));
        Assert.Equal(95, Grades.Final(history, currentGrade: 95));
    }

    [Fact]
    public void NoHistory_FinalFallsBackToTheCurrentGrade_InitialIsUnknown()
    {
        Assert.Null(Grades.Initial([]));
        Assert.Equal(80, Grades.Final([], currentGrade: 80));
        Assert.Null(Grades.Final([], currentGrade: null));
    }

    [Fact]
    public void GradeRemovedLater_FinalIsNone()
    {
        GradeEvent[] history = [Assigned(70, Day1), Assigned(null, Day1.AddDays(1))];

        Assert.Equal(70, Grades.Initial(history));
        Assert.Null(Grades.Final(history, currentGrade: null));
    }

    private static GradeEvent Assigned(double? points, DateTimeOffset at) => new(GradeEventKind.AssignedGrade, points, 100, at);
}
