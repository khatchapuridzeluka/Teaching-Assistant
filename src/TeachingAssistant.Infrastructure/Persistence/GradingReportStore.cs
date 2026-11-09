using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class GradingReportStore(AppDbContext db) : IGradingReportStore
{
    public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
        db.GradingReports
            .Include(r => r.Mistakes)
            .SingleOrDefaultAsync(r => r.GradingId == gradingId, cancellationToken);

    public async Task<IReadOnlyList<ReportScore>> GetScoresAsync(
        IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default)
    {
        if (reportIds.Count == 0)
        {
            return [];
        }

        return await db.GradingReports
            .Where(r => reportIds.Contains(r.Id))
            .Join(db.Gradings, r => r.GradingId, g => g.Id,
                (r, g) => new ReportScore(r.Id, r.Score, r.Bonus, g.Score, g.Bonus, g.MaxScore))
            .ToListAsync(cancellationToken);
    }

    public void Add(GradingReport report) => db.GradingReports.Add(report);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
