using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class GradingGuidanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeGradingPrinciples _principles = new();
    private readonly FakeCourseGuidance _courseGuidance = new();

    private GradingGuidanceService Service() => new(_principles, _courseGuidance, new FixedClock(Now));

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
        Assert.Equal(2, guidance.Rules.Count);
    }

    [Fact]
    public async Task Add_PutsTheNewPrincipleLast_AfterDeletedOnesToo_AndItIsListed()
    {
        _principles.Add(1, "First", "Read the code.");
        _principles.Add(4, "Gone", "Removed.").Delete(Now);

        var added = await Service().AddPrincipleAsync(new GradingPrincipleRequest(" Check the output ", " Compare it with the example. "),
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
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().AddPrincipleAsync(new GradingPrincipleRequest("Title", ""),
            TestContext.Current.CancellationToken));

        Assert.Empty(_principles.All);
    }

    [Fact]
    public async Task CourseGuidance_IsSavedPerCourse_Trimmed_AndAnEmptyTextRemovesIt()
    {
        var saved = await Service().SetCourseGuidanceAsync("course-1", new CourseGuidanceRequest("  Second-year students.  "),
            TestContext.Current.CancellationToken);
        var other = await Service().GetAsync("course-2", TestContext.Current.CancellationToken);

        Assert.Equal(("Second-year students.", (DateTimeOffset?)Now), (saved.CourseGuidance, saved.CourseGuidanceUpdatedAt));
        Assert.Null(other.CourseGuidance);

        var cleared = await Service().SetCourseGuidanceAsync("course-1", new CourseGuidanceRequest(" "), TestContext.Current.CancellationToken);

        Assert.Null(cleared.CourseGuidance);
        Assert.Empty(_courseGuidance.All);
    }

    [Fact]
    public async Task CourseGuidance_TooLong_IsRefused()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().SetCourseGuidanceAsync("course-1",
            new CourseGuidanceRequest(new string('x', CourseGuidance.MaxTextLength + 1)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_ChangesTitleAndText_Trimmed()
    {
        var principle = _principles.Add(1, "Old", "Old text.");

        var updated = await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest("  New  ", " New text. "),
            TestContext.Current.CancellationToken);

        Assert.Equal(("New", "New text.", Now), (updated!.Title, updated.Detail, updated.UpdatedAt));
        Assert.Equal(("New", "New text."), (principle.Title, principle.Detail));
        Assert.Equal(1, _principles.Saves);
    }

    [Fact]
    public async Task Update_WithTheSameWords_KeepsTheStoredLineBreaksAndSavesNothing()
    {
        var principle = _principles.Add(1, "Title", "Line one\n  line two");

        await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest("Title", "Line one line two"),
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
            new GradingPrincipleRequest(title, detail), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_OnlyMarksThePrincipleDeleted_AndADeletedOneCannotBeEditedOrDeletedAgain()
    {
        var principle = _principles.Add(1, "Title", "Text");

        Assert.True(await Service().DeletePrincipleAsync(principle.Id, TestContext.Current.CancellationToken));

        Assert.True(principle.IsDeleted);
        Assert.Contains(principle, _principles.All);
        Assert.False(await Service().DeletePrincipleAsync(principle.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdatePrincipleAsync(principle.Id, new GradingPrincipleRequest("A", "B"),
            TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdatePrincipleAsync(Guid.NewGuid(), new GradingPrincipleRequest("A", "B"),
            TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed class FakeGradingPrinciples : IGradingPrincipleStore
{
    public List<GradingPrinciple> All { get; } = [];

    public int Saves { get; private set; }

    public GradingPrinciple Add(int position, string title, string detail)
    {
        var principle = new GradingPrinciple(Guid.NewGuid(), position, title, detail, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        All.Add(principle);
        return principle;
    }

    public Task<IReadOnlyList<GradingPrinciple>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GradingPrinciple>>(All.Where(p => !p.IsDeleted).OrderBy(p => p.Position).ToList());

    public Task<GradingPrinciple?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(p => p.Id == id));

    public Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Count == 0 ? 0 : All.Max(p => p.Position));

    void IGradingPrincipleStore.Add(GradingPrinciple principle) => All.Add(principle);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCourseGuidance : ICourseGuidanceStore
{
    public List<CourseGuidance> All { get; } = [];

    public Task<CourseGuidance?> FindAsync(string courseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(g => g.CourseId == courseId));

    public void Add(CourseGuidance guidance) => All.Add(guidance);

    public void Remove(CourseGuidance guidance) => All.Remove(guidance);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
