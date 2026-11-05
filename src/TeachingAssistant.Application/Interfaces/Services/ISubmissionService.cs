using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Student submissions of an assignment, for the Assignment screen.
/// </summary>
public interface ISubmissionService
{
    Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);
}
