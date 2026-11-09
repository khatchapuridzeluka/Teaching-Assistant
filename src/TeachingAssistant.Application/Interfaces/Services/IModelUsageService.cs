using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IModelUsageService
{
    Task<ModelUsageResponse> GetAsync(int days, ModelCallPurpose? purpose, CancellationToken cancellationToken = default);
}
