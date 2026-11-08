using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IStoredSubmissionService
{
    Task<IReadOnlyList<StoredSubmissionResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenFileAsync(Guid contentId, CancellationToken cancellationToken = default);

    Task<ExtractionSummary> ReExtractAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CodeFileResponse>?> ListCodeAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<Stream?> OpenCodeFileAsync(Guid versionId, string path, CancellationToken cancellationToken = default);
}
