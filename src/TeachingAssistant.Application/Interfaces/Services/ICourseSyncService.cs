using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ICourseSyncService
{
    Task<CourseSyncSummary> SyncAsync(string courseId, CancellationToken cancellationToken = default);
}
