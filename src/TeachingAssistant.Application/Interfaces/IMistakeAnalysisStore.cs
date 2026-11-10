using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IMistakeAnalysisStore
{
    Task<MistakeAnalysis?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<MistakeAnalysis?> FindByClusterAsync(Guid clusterId, CancellationToken cancellationToken = default);

    void Add(MistakeAnalysis analysis);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
