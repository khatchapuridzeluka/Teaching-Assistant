using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Our stored submissions and their versions. Shaped around what fetching needs, not generic CRUD.
/// </summary>
public interface ISubmissionStore
{
    /// <summary>
    /// Returns every stored submission of an assignment with all versions and their contents loaded.
    /// Changes to the returned submissions are saved by <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
        SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Where a stored file is, by its content ID; null when there is no such content or it was not stored.</summary>
    Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default);

    /// <summary>A version's storage path by its ID; null when there is no such version.</summary>
    Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default);

    void Add(Submission submission);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
