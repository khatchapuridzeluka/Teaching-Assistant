using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Checks that the grading model is reachable through the same path grading uses.
/// </summary>
public interface IModelHealthService
{
    /// <summary>
    /// Sends a small test prompt to the model. Never throws for model failures;
    /// returns <c>Healthy = false</c> with the error instead.
    /// </summary>
    Task<ModelHealthResponse> CheckAsync(CancellationToken cancellationToken = default);
}
