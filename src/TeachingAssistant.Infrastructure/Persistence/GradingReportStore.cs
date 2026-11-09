using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class GradingReportStore(AppDbContext db) : IGradingReportStore
{
    public Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
        db.GradingReports
            .Include(r => r.Mistakes)
            .SingleOrDefaultAsync(r => r.GradingId == gradingId, cancellationToken);

    public void Add(GradingReport report) => db.GradingReports.Add(report);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
