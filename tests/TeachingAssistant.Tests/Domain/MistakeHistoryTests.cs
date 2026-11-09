using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Domain;

public class MistakeHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, DateTimeOffset?> _dueDates = new()
    {
        ["a1"] = Now.AddDays(-30),
        ["a2"] = Now.AddDays(-20),
        ["a3"] = Now.AddDays(-10),
    };

    [Fact]
    public void ACategoryFromAnEarlierAssignment_IsRepeated_TheFirstTimeIsNew()
    {
        var mistakes = Approve("a1", (MistakeCategory.Naming, 3), (MistakeCategory.Design, 1))
            .Concat(Approve("a2", (MistakeCategory.Naming, 3), (MistakeCategory.Correctness, 5)))
            .ToList();

        var labeled = MistakeHistory.Label(mistakes, DueDate).ToDictionary(l => (l.Mistake.AssignmentId, l.Mistake.Category));

        Assert.Equal(MistakeLabel.New, labeled[("a1", MistakeCategory.Naming)].Label);
        Assert.Equal((MistakeLabel.Repeated, "a1"), (labeled[("a2", MistakeCategory.Naming)].Label, labeled[("a2", MistakeCategory.Naming)].FirstAssignmentId));
        Assert.Equal(MistakeLabel.New, labeled[("a2", MistakeCategory.Correctness)].Label);
    }

    [Fact]
    public void EarlierMeansTheAssignmentsDueDate_NotWhenItWasApproved()
    {
        var later = Approve("a3", (MistakeCategory.Naming, 3));
        var earlier = Approve("a1", (MistakeCategory.Naming, 3));

        var labeled = MistakeHistory.Label(later.Concat(earlier), DueDate).ToDictionary(l => l.Mistake.AssignmentId);

        Assert.Equal(MistakeLabel.New, labeled["a1"].Label);
        Assert.Equal(MistakeLabel.Repeated, labeled["a3"].Label);
    }

    [Fact]
    public void TheSameCategoryTwiceInOneAssignment_IsNotRepeated()
    {
        var labeled = MistakeHistory.Label(Approve("a1", (MistakeCategory.Naming, 3), (MistakeCategory.Naming, 3)), DueDate);

        Assert.All(labeled, l => Assert.Equal(MistakeLabel.New, l.Label));
    }

    [Fact]
    public void Trends_SumPointsPerCategory_MostCostlyFirst_AndFlagRepeatedOnes()
    {
        var mistakes = Approve("a1", (MistakeCategory.Naming, 3), (MistakeCategory.Readability, 0))
            .Concat(Approve("a2", (MistakeCategory.Naming, 3), (MistakeCategory.Correctness, 5)));

        var trends = MistakeHistory.Trends(mistakes, DueDate);

        Assert.Equal([MistakeCategory.Naming, MistakeCategory.Correctness, MistakeCategory.Readability], trends.Select(t => t.Category));
        Assert.Equal((2, 2, true, 6.0, "a1", "a2"), (trends[0].Count, trends[0].Assignments, trends[0].Repeated, trends[0].PointsLost,
            trends[0].FirstAssignmentId, trends[0].LastAssignmentId));
        Assert.False(trends[1].Repeated);
    }

    private DateTimeOffset? DueDate(string assignmentId) => _dueDates.GetValueOrDefault(assignmentId);

    private static IReadOnlyList<StudentMistake> Approve(string assignmentId, params (MistakeCategory Category, double Points)[] mistakes)
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", assignmentId, "student", "Ana", $"sub-{assignmentId}", "folder", Now);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "path", Now, Now, []);
        var grading = new AiGrading(version.Id, "model", "review", 80, 100, null, 1, [], [], "{}",
            mistakes.Select(m => new GradingMistake("A.java", 1, 1, MistakeSeverity.Minor, m.Category, $"{m.Category} problem", m.Points)), Now);
        var report = new GradingReport(grading, submission, Now);
        report.Approve(Now);
        return StudentMistake.FromApprovedReport(report, submission);
    }
}
