using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IInstructionsService
{
    Task<StoredInstructionsResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenFileAsync(string assignmentId, Guid contentId, CancellationToken cancellationToken = default);
}
