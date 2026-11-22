using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

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
