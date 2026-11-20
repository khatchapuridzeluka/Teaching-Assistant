using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class EvaluationRunStore(AppDbContext db) : IEvaluationRunStore
{
    public void Add(EvaluationRun run, IEnumerable<EvaluationRunResult> results)
    {
        db.EvaluationRuns.Add(run);
        db.EvaluationRunResults.AddRange(results);
    }

    public async Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await db.EvaluationRunResults.Where(r => r.RunId == runId).ExecuteDeleteAsync(cancellationToken);
        await db.EvaluationRuns.Where(r => r.Id == runId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<EvaluationRunResult?> FindResultAsync(Guid resultId, CancellationToken cancellationToken = default)
    {
        return db.EvaluationRunResults.SingleOrDefaultAsync(r => r.Id == resultId, cancellationToken);
    }

    public Task<EvaluationRun?> GetLatestAsync(string? courseId, CancellationToken cancellationToken = default)
    {
        return db.EvaluationRuns
            .Where(r => courseId == null || r.CourseId == courseId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationRunResult>> GetResultsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await db.EvaluationRunResults.Where(r => r.RunId == runId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationRunResult>> GetDoneAsync(CancellationToken cancellationToken = default)
    {
        return await db.EvaluationRunResults.Where(r => r.Status == RunResultStatus.Done).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvaluationRunResult>> GetFailedAsync(CancellationToken cancellationToken = default)
    {
        return await db.EvaluationRunResults.Where(r => r.Status == RunResultStatus.Failed).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
