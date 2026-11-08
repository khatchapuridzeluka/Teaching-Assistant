using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingService
{
    Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<GradableVersion?> CheckAsync(Guid versionId, CancellationToken cancellationToken = default);
}

public interface IGradingDraftService
{
    Task<GradingResponse?> GetCurrentAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradingResponse>> GetHistoryAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentGradingResponse>> GetByCourseAsync(
        string courseId, string? assignmentId, string? studentId, bool includeAll, CancellationToken cancellationToken = default);

    Task<GradingResponse?> DiscardAsync(Guid gradingId, CancellationToken cancellationToken = default);
}
