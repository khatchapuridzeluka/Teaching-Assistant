using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Infrastructure.Persistence;

public class ModelCallStore(AppDbContext db, ILogger<ModelCallStore> logger) : IModelCallStore
{
    public async Task RecordAsync(ModelCall call, CancellationToken cancellationToken = default)
    {
        var entry = db.ModelCalls.Add(call);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            entry.State = EntityState.Detached;
            logger.LogWarning(ex, "Could not record a {Purpose} model call; the call itself is not affected", call.Purpose);
        }
    }

    public async Task<IReadOnlyList<ModelCall>> GetSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default) =>
        await db.ModelCalls
            .AsNoTracking()
            .Where(c => c.CreatedAt >= since)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
}
