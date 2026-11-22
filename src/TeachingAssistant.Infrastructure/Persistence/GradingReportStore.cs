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
            .Include(r => r.Strengths)
            .AsSplitQuery()
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
                (r, g) => new ReportScore
                {
                    ReportId = r.Id,
                    Score = r.Score,
                    Bonus = r.Bonus,
                    AiScore = g.Score,
                    AiBonus = g.Bonus,
                    MaxScore = g.MaxScore,
                })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default)
    {
        var reports = await db.GradingReports
            .Include(r => r.Mistakes)
            .Include(r => r.Strengths)
            .AsSplitQuery()
            .Where(r => r.Status == ReportStatus.Approved && (courseId == null || r.CourseId == courseId))
            .ToListAsync(cancellationToken);
        if (reports.Count == 0)
        {
            return [];
        }

        var gradingIds = reports.Select(r => r.GradingId).ToList();
        var drafts = await db.Gradings
            .Include(g => g.Mistakes)
            .Include(g => g.Strengths)
            .AsSplitQuery()
            .Where(g => gradingIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        return reports
            .Where(r => drafts.ContainsKey(r.GradingId))
            .Select(r => new ApprovedReport { Report = r, Draft = drafts[r.GradingId] })
            .ToList();
    }

    public void Add(GradingReport report) => db.GradingReports.Add(report);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
