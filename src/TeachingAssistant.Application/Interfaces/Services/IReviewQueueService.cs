using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IReviewQueueService
{
    Task<ReviewQueueResponse> GetAsync(CancellationToken cancellationToken = default);
}
