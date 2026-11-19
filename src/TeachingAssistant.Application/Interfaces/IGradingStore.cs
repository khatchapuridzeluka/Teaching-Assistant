using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface IGradingStore
{
    Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default);

    Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default);

    /// <summary>Every discarded draft, newest discard first, in one course or in all of them (courseId null).</summary>
    Task<IReadOnlyList<DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId,
        CancellationToken cancellationToken = default);

    void Add(AiGrading grading);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
