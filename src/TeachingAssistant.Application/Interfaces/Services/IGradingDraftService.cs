using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingDraftService
{
    Task<GradingResponse?> GetCurrentAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradingResponse>> GetHistoryAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentGradingResponse>> GetByCourseAsync(
        string courseId, string? assignmentId, string? studentId, bool includeAll, CancellationToken cancellationToken = default);

    /// <summary>Discards a draft with the teacher's reason (none for older callers); null when there is no such draft.</summary>
    Task<GradingResponse?> DiscardAsync(Guid gradingId, DiscardGradingRequest? request = null, CancellationToken cancellationToken = default);
}
