using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class PolicyRuleStore(AppDbContext db) : IPolicyRuleStore
{
    public async Task<IReadOnlyList<PolicyRule>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.PolicyRules
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Position)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<PolicyRule?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return db.PolicyRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default)
    {
        return await db.PolicyRules.MaxAsync(r => (int?)r.Position, cancellationToken) ?? 0;
    }

    public void Add(PolicyRule rule)
    {
        db.PolicyRules.Add(rule);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
