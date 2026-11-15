using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

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

        var added = await Service().AddAsync("course-1", "a-1", new AssignmentNoteRequest(" Strategy pattern ", " Deduct if it is missing. "),
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

        await Service().UpdateAsync(note.Id, new AssignmentNoteRequest("Title", "Line one line two"), TestContext.Current.CancellationToken);
        Assert.Equal(0, _notes.Saves);

        var updated = await Service().UpdateAsync(note.Id, new AssignmentNoteRequest("New", "New text."), TestContext.Current.CancellationToken);
        Assert.Equal(("New", "New text.", Now), (updated!.Title, updated.Detail, updated.UpdatedAt));
    }

    [Fact]
    public async Task Delete_OnlyMarksItDeleted_AndEmptyInputIsRefused()
    {
        var note = _notes.Add("a-1", 1, "Title", "Text");

        Assert.True(await Service().DeleteAsync(note.Id, TestContext.Current.CancellationToken));
        Assert.True(note.IsDeleted);
        Assert.False(await Service().DeleteAsync(note.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Service().UpdateAsync(note.Id, new AssignmentNoteRequest("A", "B"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().AddAsync("course-1", "a-1", new AssignmentNoteRequest("Title", " "), TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

internal sealed class FakeAssignmentNotes : IAssignmentNoteStore
{
    public List<AssignmentNote> All { get; } = [];

    public int Saves { get; private set; }

    public AssignmentNote Add(string assignmentId, int position, string title, string detail)
    {
        var note = new AssignmentNote(Guid.NewGuid(), "course-1", assignmentId, position, title, detail,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        All.Add(note);
        return note;
    }

    public Task<IReadOnlyList<AssignmentNote>> GetActiveAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssignmentNote>>(
            All.Where(n => n.AssignmentId == assignmentId && !n.IsDeleted).OrderBy(n => n.Position).ToList());

    public Task<IReadOnlyDictionary<string, int>> CountByAssignmentAsync(string courseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(All.Where(n => n.CourseId == courseId && !n.IsDeleted)
            .GroupBy(n => n.AssignmentId).ToDictionary(g => g.Key, g => g.Count()));

    public Task<AssignmentNote?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(n => n.Id == id));

    public Task<int> GetLastPositionAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Where(n => n.AssignmentId == assignmentId).Select(n => n.Position).DefaultIfEmpty(0).Max());

    void IAssignmentNoteStore.Add(AssignmentNote note) => All.Add(note);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
