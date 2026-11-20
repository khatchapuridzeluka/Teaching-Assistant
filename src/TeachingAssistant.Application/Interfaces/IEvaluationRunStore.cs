using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IEvaluationRunStore
{
    void Add(EvaluationRun run, IEnumerable<EvaluationRunResult> results);

    /// <summary>Deletes a run and its results right away, for a run whose gradings could not be queued.</summary>
    Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<EvaluationRunResult?> FindResultAsync(Guid resultId, CancellationToken cancellationToken = default);

    /// <summary>The newest run in one course, or the newest run over all courses (courseId null).</summary>
    Task<EvaluationRun?> GetLatestAsync(string? courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationRunResult>> GetResultsAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationRunResult>> GetDoneAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationRunResult>> GetFailedAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
