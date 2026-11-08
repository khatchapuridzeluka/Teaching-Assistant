using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IModelHealthService
{
    Task<ModelHealthResponse> CheckAsync(CancellationToken cancellationToken = default);
}
