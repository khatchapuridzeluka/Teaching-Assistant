using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IModelUsageService
{
    Task<ModelUsageResponse> GetAsync(int days, CancellationToken cancellationToken = default);
}
