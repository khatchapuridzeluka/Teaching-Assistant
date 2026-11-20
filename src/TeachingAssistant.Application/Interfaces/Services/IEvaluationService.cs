using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IEvaluationService
{
    /// <summary>Compares every approved report with its AI draft, in one course or in all of them (courseId null).</summary>
    /// <summary>The drafts you reviewed against your approved reports, or, with a model, that model's side gradings against them.</summary>
    Task<EvaluationResponse> GetAsync(string? courseId = null, string? model = null, CancellationToken cancellationToken = default);
}
