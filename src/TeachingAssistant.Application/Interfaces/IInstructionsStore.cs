using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

public interface IInstructionsStore
{
    Task<AssignmentInstructions?> GetAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default);

    void Add(AssignmentInstructions instructions);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
