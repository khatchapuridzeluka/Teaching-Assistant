using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Saves an assignment's instructions (task statement and attached files) as a new version when they changed since the
/// last fetch, so grading reads a stored copy. Called by the submission fetch, before the submissions.
/// </summary>
public interface IInstructionsFetchService
{
    /// <summary>
    /// Compares the instructions the source reports now with the latest stored version and stores a new version only when
    /// something changed. Throws <see cref="Exceptions.SubmissionSourceNotFoundException"/> or
    /// <see cref="Exceptions.SubmissionSourceUnavailableException"/> when a file cannot be read; nothing is stored then.
    /// </summary>
    Task<InstructionsFetchResult> FetchAsync(
        string courseId, string courseName, AssignmentResponse assignment, CancellationToken cancellationToken = default);
}
