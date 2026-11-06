using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary>
/// <see cref="IInstructionsStore"/> on EF Core. Instructions are loaded with their whole version history, which is small
/// and needed for change detection.
/// </summary>
public class InstructionsStore(AppDbContext db) : IInstructionsStore
{
    public Task<AssignmentInstructions?> GetAsync(
        SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
        db.AssignmentInstructions
            .Include(i => i.Versions)
            .ThenInclude(v => v.Contents)
            .Where(i => i.Source == source && i.AssignmentId == assignmentId)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(AssignmentInstructions instructions) => db.AssignmentInstructions.Add(instructions);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
