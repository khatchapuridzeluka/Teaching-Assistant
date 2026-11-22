using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class GradingGuidanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeGradingPrinciples _principles = new();
    private readonly FakeCourseGuidance _courseGuidance = new();
    private readonly FakePolicyRules _rules = new();
    private readonly FakeAssignmentNotes _notes = new();

    private GradingGuidanceService Service() => new(_principles, _courseGuidance, _rules, _notes, new FixedClock(Now));

    [Fact]
    public async Task Get_ListsTheActivePrinciplesInOrder_AsOneParagraphEach()
    {
        var first = _principles.Add(2, "Second", "Line one\n  line two");
        _principles.Add(1, "First", "Read the code.");
        _principles.Add(3, "Gone", "Removed.").Delete(Now);

        var guidance = await Service().GetAsync("course-1", TestContext.Current.CancellationToken);

        Assert.Equal(["First", "Second"], guidance.Approach.Select(p => p.Title));
        Assert.Equal("Line one line two", guidance.Approach[1].Detail);
        Assert.Equal(first.Id, guidance.Approach[1].Id);
        Assert.Empty(guidance.Rules);
        Assert.Empty(guidance.Notes);
    }

    [Fact]
    public async Task Add_PutsTheNewPrincipleLast_AfterDeletedOnesToo_AndItIsListed()
    {
        _principles.Add(1, "First", "Read the code.");
        _principles.Add(4, "Gone", "Removed.").Delete(Now);

        var added = await Service().AddPrincipleAsync(new GradingPrincipleRequest
        {
            Title = " Check the output ",
            Detail = " Compare it with the example. ",
        },
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(_principles.All, p => p.Id == added.Id);
        Assert.Equal((5, "Check the output", "Compare it with the example.", false), (stored.Position, stored.Title, stored.Detail, stored.IsDeleted));
        Assert.Equal(1, _principles.Saves);
        var guidance = await Service().GetAsync("course-1", TestContext.Current.CancellationToken);
        Assert.Equal(["First", "Check the output"], guidance.Approach.Select(p => p.Title));
    }

    [Fact]
    public async Task Add_WithoutATitleOrText_IsRefused_AndNothingIsStored()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().AddPrincipleAsync(new GradingPrincipleRequest
        {
            Title = "Title",
            Detail = "",
        },
            TestContext.Current.CancellationToken));

        Assert.Empty(_principles.All);
    }

    [Fact]
    public async Task CourseGuidance_IsSavedPerCourse_Trimmed_AndAnEmptyTextRemovesIt()
    {
        var saved = await Service().SetCourseGuidanceAsync("course-1", new CourseGuidanceRequest
        {
            Text = "  Second-year students.  ",
        },
            TestContext.Current.CancellationToken);
        var other = await Service().GetAsync("course-2", TestContext.Current.CancellationToken);

        Assert.Equal(("Second-year students.", (DateTimeOffset?)Now), (saved.CourseGuidance, saved.CourseGuidanceUpdatedAt));
        Assert.Null(other.CourseGuidance);

        var cleared = await Service().SetCourseGuidanceAsync("course-1", new CourseGuidanceRequest { Text = " " }, TestContext.Current.CancellationToken);

        Assert.Null(cleared.CourseGuidance);
        Assert.Empty(_courseGuidance.All);
    }

    [Fact]
    public async Task CourseGuidance_TooLong_IsRefused()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().SetCourseGuidanceAsync("course-1",
            new CourseGuidanceRequest { Text = new string('x', CourseGuidance.MaxTextLength + 1) }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddRule_PutsItLast_Trimmed_AndANoDeductionRuleKeepsNoPoints()
    {
        _rules.Add(3, "Old", "Removed.").Delete(Now);

        var added = await Service().AddRuleAsync(
            new PolicyRuleRequest
            {
                Title = " Missing output ",
                Effect = PolicyEffect.NoDeduction,
                Points = 5,
                PointsPer = "per line",
                Covers = " Printed text ",
                Detail = " No points for printing. ",
            },
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(_rules.All, r => r.Id == added.Id);
        Assert.Equal((4, "Missing output", PolicyEffect.NoDeduction, (double?)null, (string?)null, "Printed text", "No points for printing."),
            (stored.Position, stored.Title, stored.Effect, stored.Points, stored.PointsPer, stored.Covers, stored.Detail));
        var guidance = await Service().GetAsync("course-1", TestContext.Current.CancellationToken);
        Assert.Equal(["Missing output"], guidance.Rules.Select(r => r.Title));
    }

    [Theory]
    [InlineData(PolicyEffect.Deduction, null)]
    [InlineData(PolicyEffect.Deduction, 0.0)]
    [InlineData(PolicyEffect.Deduction, 101.0)]
    public async Task ADeductionWithoutValidPoints_IsRefused(PolicyEffect effect, double? points)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().AddRuleAsync(
            new PolicyRuleRequest { Title = "Names", Effect = effect, Points = points, Covers = "Names", Detail = "Deduct." }, TestContext.Current.CancellationToken));

        Assert.Empty(_rules.All);
    }

    [Fact]
    public async Task UpdateRule_ChangesEverything_AndSavingTheSameRuleAgainChangesNothing()
    {
        var rule = _rules.Add(1, "Names", "Line one\n  line two");

        var updated = await Service().UpdateRuleAsync(rule.Id,
            new PolicyRuleRequest
            {
                Title = "Names",
                Effect = PolicyEffect.Deduction,
                Points = 2,
                PointsPer = "per name",
                Covers = "Class and method names",
                Detail = "Deduct 2 points per name.",
            },
            TestContext.Current.CancellationToken);
        var saves = _rules.Saves;
        await Service().UpdateRuleAsync(rule.Id,
            new PolicyRuleRequest
            {
                Title = "Names",
                Effect = PolicyEffect.Deduction,
                Points = 2,
                PointsPer = "per name",
                Covers = "Class and method names",
                Detail = "Deduct 2 points per name.",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal((PolicyEffect.Deduction, (double?)2, "per name", "Class and method names", Now),
            (updated!.Effect, updated.Points, updated.PointsPer, updated.Covers, updated.UpdatedAt));
        Assert.Equal(saves, _rules.Saves);
    }

    [Fact]
    public async Task DeleteRule_OnlyMarksItDeleted_AndADeletedRuleCannotBeEditedOrDeletedAgain()
    {
        var rule = _rules.Add(1, "Names", "Deduct.");

        Assert.True(await Service().DeleteRuleAsync(rule.Id, TestContext.Current.CancellationToken));

        Assert.True(rule.IsDeleted);
        Assert.Contains(rule, _rules.All);
        Assert.False(await Service().DeleteRuleAsync(rule.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdateRuleAsync(rule.Id,
            new PolicyRuleRequest { Title = "A", Effect = PolicyEffect.NoDeduction, Covers = "B", Detail = "C" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_ChangesTitleAndText_Trimmed()
    {
        var principle = _principles.Add(1, "Old", "Old text.");

        var updated = await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest
        {
            Title = "  New  ",
            Detail = " New text. ",
        },
            TestContext.Current.CancellationToken);

        Assert.Equal(("New", "New text.", Now), (updated!.Title, updated.Detail, updated.UpdatedAt));
        Assert.Equal(("New", "New text."), (principle.Title, principle.Detail));
        Assert.Equal(1, _principles.Saves);
    }

    [Fact]
    public async Task Update_WithTheSameWords_KeepsTheStoredLineBreaksAndSavesNothing()
    {
        var principle = _principles.Add(1, "Title", "Line one\n  line two");

        await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest
        {
            Title = "Title",
            Detail = "Line one line two",
        },
            TestContext.Current.CancellationToken);

        Assert.Equal("Line one\n  line two", principle.Detail);
        Assert.Equal(0, _principles.Saves);
    }

    [Theory]
    [InlineData("", "Text")]
    [InlineData("Title", "  ")]
    public async Task Update_WithoutTitleOrText_IsRefused(string title, string detail)
    {
        var principle = _principles.Add(1, "Title", "Text");

        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().UpdatePrincipleAsync(principle.Id,
            new GradingPrincipleRequest { Title = title, Detail = detail }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_OnlyMarksThePrincipleDeleted_AndADeletedOneCannotBeEditedOrDeletedAgain()
    {
        var principle = _principles.Add(1, "Title", "Text");

        Assert.True(await Service().DeletePrincipleAsync(principle.Id, TestContext.Current.CancellationToken));

        Assert.True(principle.IsDeleted);
        Assert.Contains(principle, _principles.All);
        Assert.False(await Service().DeletePrincipleAsync(principle.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest { Title = "A", Detail = "B" },
            TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdatePrincipleAsync(Guid.NewGuid(), new GradingPrincipleRequest { Title = "A", Detail = "B" },
            TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
