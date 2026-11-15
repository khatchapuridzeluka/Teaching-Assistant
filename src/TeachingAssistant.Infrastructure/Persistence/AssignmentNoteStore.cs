using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class AssignmentNoteStore(AppDbContext db) : IAssignmentNoteStore
{
    public async Task<IReadOnlyList<AssignmentNote>> GetActiveAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        return await db.AssignmentNotes
            .Where(n => n.AssignmentId == assignmentId && !n.IsDeleted)
            .OrderBy(n => n.Position)
            .ThenBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByAssignmentAsync(string courseId, CancellationToken cancellationToken = default)
    {
        return await db.AssignmentNotes
            .Where(n => n.CourseId == courseId && !n.IsDeleted)
            .GroupBy(n => n.AssignmentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
    }

    public Task<AssignmentNote?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.AssignmentNotes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<int> GetLastPositionAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        return await db.AssignmentNotes.Where(n => n.AssignmentId == assignmentId).MaxAsync(n => (int?)n.Position, cancellationToken) ?? 0;
    }

    public void Add(AssignmentNote note)
    {
        db.AssignmentNotes.Add(note);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
