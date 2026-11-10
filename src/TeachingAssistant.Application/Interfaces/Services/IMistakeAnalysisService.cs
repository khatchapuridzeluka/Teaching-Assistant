using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IMistakeAnalysisService
{
    Task<Guid> AnalyzeAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<CommonMistakesResponse?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<MistakeClusterResponse?> ApproveAsync(Guid clusterId, string explanation, CancellationToken cancellationToken = default);
}
