using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// What fetch has stored: submissions with their versions and files, the files themselves for preview,
/// and the extracted code of each version.
/// </summary>
public interface IStoredSubmissionService
{
    Task<IReadOnlyList<StoredSubmissionResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file by its content ID, or returns null when it does not exist.</summary>
    Task<StoredFileContent?> OpenFileAsync(Guid contentId, CancellationToken cancellationToken = default);

    /// <summary>Rebuilds the code folder of every stored version of an assignment from its originals.</summary>
    Task<ExtractionSummary> ReExtractAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Lists a version's extracted code files, or returns null when the version does not exist.</summary>
    Task<IReadOnlyList<CodeFileResponse>?> ListCodeAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>Opens one extracted code file, or returns null when the version or file does not exist.</summary>
    Task<Stream?> OpenCodeFileAsync(Guid versionId, string path, CancellationToken cancellationToken = default);
}
