using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IEvaluationService
{
    /// <summary>Compares every approved report with its AI draft, in one course or in all of them (courseId null).</summary>
    Task<EvaluationResponse> GetAsync(string? courseId = null, CancellationToken cancellationToken = default);
}
