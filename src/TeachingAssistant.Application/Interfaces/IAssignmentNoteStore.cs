using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IAssignmentNoteStore
{
    /// <summary>The assignment's notes not deleted, in order.</summary>
    Task<IReadOnlyList<AssignmentNote>> GetActiveAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>How many notes (not deleted) each assignment of the course has; assignments without notes are left out.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByAssignmentAsync(string courseId, CancellationToken cancellationToken = default);

    Task<AssignmentNote?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The highest position used in the assignment so far, deleted notes included (0 when there are none).</summary>
    Task<int> GetLastPositionAsync(string assignmentId, CancellationToken cancellationToken = default);

    void Add(AssignmentNote note);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
