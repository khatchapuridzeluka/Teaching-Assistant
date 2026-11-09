using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface IReviewQueueStore
{
    Task<IReadOnlyList<LatestVersionRow>> GetLatestVersionsAsync(SubmissionSource source, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DraftRow>> GetDraftsAsync(SubmissionSource source, CancellationToken cancellationToken = default);
}
