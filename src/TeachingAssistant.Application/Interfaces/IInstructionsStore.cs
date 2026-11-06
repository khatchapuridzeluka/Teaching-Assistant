using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Our stored assignment instructions and their versions. Shaped around fetching and grading, not generic CRUD.
/// </summary>
public interface IInstructionsStore
{
    /// <summary>
    /// The instructions of an assignment with all versions and their contents loaded, or null before the first fetch.
    /// Changes to the returned instructions are saved by <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<AssignmentInstructions?> GetAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default);

    void Add(AssignmentInstructions instructions);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
