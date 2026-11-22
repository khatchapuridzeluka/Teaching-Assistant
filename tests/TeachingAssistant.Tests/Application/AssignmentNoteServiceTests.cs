using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Services;

namespace TeachingAssistant.Tests.Application;

public class AssignmentNoteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAssignmentNotes _notes = new();

    private AssignmentNoteService Service() => new(_notes, new FixedClock(Now));

    [Fact]
    public async Task Add_NumbersNotesPerAssignment_AfterDeletedOnesToo()
    {
        _notes.Add("a-1", 1, "Old", "Removed.").Delete(Now);
        _notes.Add("a-2", 5, "Other", "Another assignment.");

        var added = await Service().AddAsync("course-1", "a-1", new AssignmentNoteRequest
        {
            Title = " Strategy pattern ",
            Detail = " Deduct if it is missing. ",
        },
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(_notes.All, n => n.Id == added.Id);
        Assert.Equal(("course-1", "a-1", 2, "Strategy pattern", "Deduct if it is missing."),
            (stored.CourseId, stored.AssignmentId, stored.Position, stored.Title, stored.Detail));
        Assert.Equal(["Strategy pattern"], (await Service().GetAsync("a-1", TestContext.Current.CancellationToken)).Select(n => n.Title));
    }

    [Fact]
    public async Task Update_ChangesTheNote_AndTheSameWordsChangeNothing()
    {
        var note = _notes.Add("a-1", 1, "Title", "Line one\n  line two");

        await Service().UpdateAsync(note.Id, new AssignmentNoteRequest { Title = "Title", Detail = "Line one line two" }, TestContext.Current.CancellationToken);
        Assert.Equal(0, _notes.Saves);

        var updated = await Service().UpdateAsync(note.Id, new AssignmentNoteRequest { Title = "New", Detail = "New text." }, TestContext.Current.CancellationToken);
        Assert.Equal(("New", "New text.", Now), (updated!.Title, updated.Detail, updated.UpdatedAt));
    }

    [Fact]
    public async Task Delete_OnlyMarksItDeleted_AndEmptyInputIsRefused()
    {
        var note = _notes.Add("a-1", 1, "Title", "Text");

        Assert.True(await Service().DeleteAsync(note.Id, TestContext.Current.CancellationToken));
        Assert.True(note.IsDeleted);
        Assert.False(await Service().DeleteAsync(note.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdateAsync(note.Id, new AssignmentNoteRequest { Title = "A", Detail = "B" }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().AddAsync("course-1", "a-1", new AssignmentNoteRequest { Title = "Title", Detail = " " }, TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
