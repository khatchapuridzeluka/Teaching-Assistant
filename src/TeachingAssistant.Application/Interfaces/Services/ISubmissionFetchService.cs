using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Downloads and stores the submissions of an assignment as versions, skipping what has not changed.
/// </summary>
public interface ISubmissionFetchService
{
    Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);
}
