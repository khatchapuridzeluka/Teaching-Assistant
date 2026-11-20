using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IEvaluationRunService
{
    /// <summary>Starts a run: one side grading per approved report and model, queued in the background.</summary>
    Task<ModelComparisonResponse> StartAsync(StartEvaluationRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>The models compared so far and the newest run's progress, in one course or all (courseId null).</summary>
    Task<ModelComparisonResponse> GetAsync(string? courseId = null, CancellationToken cancellationToken = default);
}
