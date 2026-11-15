using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class GradingPrincipleStore(AppDbContext db) : IGradingPrincipleStore
{
    public async Task<IReadOnlyList<GradingPrinciple>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.GradingPrinciples
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Position)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<GradingPrinciple?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.GradingPrinciples.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default)
    {
        return await db.GradingPrinciples.MaxAsync(p => (int?)p.Position, cancellationToken) ?? 0;
    }

    public void Add(GradingPrinciple principle)
    {
        db.GradingPrinciples.Add(principle);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
