using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>Copies a course's roster, assignments and grades from the source into our database. Downloads no files.</summary>
public interface ICourseSyncService
{
    Task<CourseSyncSummary> SyncAsync(string courseId, CancellationToken cancellationToken = default);
}
