using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// The assignment instructions fetch has stored, for the teacher to read what grading sends to the model.
/// </summary>
public interface IInstructionsService
{
    /// <summary>The newest stored instructions version, or null before the first fetch.</summary>
    Task<StoredInstructionsResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored instructions file of this assignment, or returns null when it does not exist.</summary>
    Task<StoredFileContent?> OpenFileAsync(string assignmentId, Guid contentId, CancellationToken cancellationToken = default);
}
