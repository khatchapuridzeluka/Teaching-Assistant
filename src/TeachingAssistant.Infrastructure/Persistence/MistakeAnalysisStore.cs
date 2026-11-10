using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class MistakeAnalysisStore(AppDbContext db) : IMistakeAnalysisStore
{
    public Task<MistakeAnalysis?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
        db.MistakeAnalyses
            .Include(a => a.Clusters)
            .ThenInclude(c => c.Members)
            .Where(a => a.CourseId == courseId && a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.CreatedAt)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<MistakeAnalysis?> FindByClusterAsync(Guid clusterId, CancellationToken cancellationToken = default) =>
        db.MistakeAnalyses
            .Include(a => a.Clusters)
            .ThenInclude(c => c.Members)
            .Where(a => a.Clusters.Any(c => c.Id == clusterId))
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(MistakeAnalysis analysis) => db.MistakeAnalyses.Add(analysis);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
