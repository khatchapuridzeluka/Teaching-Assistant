using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface ISubmissionFetchService
{
    Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);
}
