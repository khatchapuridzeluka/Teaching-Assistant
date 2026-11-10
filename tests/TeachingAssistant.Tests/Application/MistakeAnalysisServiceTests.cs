using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class MistakeAnalysisServiceTests
{
    private static readonly IReadOnlyList<ClusterMember> Mistakes =
    [
        Member("Ana", MistakeSeverity.Major, MistakeCategory.Encapsulation, "Field costs is public."),
        Member("Beka", MistakeSeverity.Major, MistakeCategory.Encapsulation, "Make the fields private."),
        Member("Ana", MistakeSeverity.Minor, MistakeCategory.Naming, "getCyear should be getConstructionYear."),
        Member("Dato", MistakeSeverity.Style, MistakeCategory.Readability, "Unused import."),
    ];

    private static readonly IReadOnlyList<MistakeAnalysisService.StudentScore> Scores =
    [
        new("id-Ana", 82, 100, 5, 2),
        new("id-Beka", 90, 100, null, 1),
        new("id-Dato", null, 100, null, 1),
    ];

    [Fact]
    public void Prompt_GivesScoresAndEveryMistakeByNumberUnderItsSeverity_TaggedByStudent_WithoutNames()
    {
        var prompt = MistakeAnalysisService.Prompt(Mistakes, Scores);

        Assert.Contains("<scores>", prompt);
        Assert.Contains("S1: 82 / 100 (+5 bonus), 2 mistakes", prompt);
        Assert.Contains("S3: no score / 100, 1 mistake", prompt);
        Assert.Contains("<mistakes severity=\"Major\">", prompt);
        Assert.Contains("[1] S1 · Encapsulation · -3 pts · Animal.java:4 · Field costs is public.", prompt);
        Assert.Contains("[2] S2 · Encapsulation", prompt);
        Assert.Contains("<mistakes severity=\"Style\">", prompt);
        Assert.True(prompt.IndexOf("[3]", StringComparison.Ordinal) > prompt.IndexOf("severity=\"Minor\"", StringComparison.Ordinal));
        Assert.DoesNotContain("Ana", prompt);
        Assert.DoesNotContain("Beka", prompt);
    }

    [Fact]
    public void Cluster_UsesEachMistakeOnce_DropsUnknownNumbers_AndKeepsTheRestAsNotGrouped()
    {
        var groups = new List<MistakeAnalysisService.ReplyGroup>
        {
            new("Fields left public", "Make fields private.", [1, 2, 99]),
            new("Same again", "Duplicate.", [2]),
            new(" ", "No title.", [3]),
        };

        var clusters = MistakeAnalysisService.Cluster(Mistakes, groups);

        Assert.Equal(2, clusters.Count);
        Assert.Equal(("Fields left public", 2), (clusters[0].Title, clusters[0].Members.Count));
        Assert.True(clusters[1].Ungrouped);
        Assert.Equal(["getCyear should be getConstructionYear.", "Unused import."], clusters[1].Members.Select(m => m.Message));
    }

    [Fact]
    public void AnExplanation_MustNotBeEmptyToApprove()
    {
        var cluster = new MistakeCluster("Title", "AI text", false, [Member("Ana", MistakeSeverity.Minor, MistakeCategory.Other, "x")]);

        Assert.Throws<ArgumentException>(() => cluster.Approve(" ", DateTimeOffset.UtcNow));
        cluster.Approve("My text", DateTimeOffset.UtcNow);
        Assert.Equal(("AI text", "My text"), (cluster.Explanation, cluster.ApprovedExplanation));
    }

    private static ClusterMember Member(string student, MistakeSeverity severity, MistakeCategory category, string message) =>
        new(Guid.NewGuid(), Guid.NewGuid(), $"id-{student}", student, $"sub-{student}", severity, category,
            severity == MistakeSeverity.Major ? 3 : null, message, severity == MistakeSeverity.Major ? "src/Animal.java" : null,
            severity == MistakeSeverity.Major ? 4 : null, null);
}
