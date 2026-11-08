using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface ISubmissionStore
{
    Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
        SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Submission>> GetByCourseAsync(
        SubmissionSource source, string courseId, CancellationToken cancellationToken = default);

    Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default);

    Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default);

    void Add(Submission submission);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
