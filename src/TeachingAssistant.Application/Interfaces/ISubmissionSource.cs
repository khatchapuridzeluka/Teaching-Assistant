using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Where courses, assignments and student submissions come from (Google Classroom first; zip upload and GitHub later).
/// Read-only: a source never writes grades or changes anything.
/// Throws <see cref="Exceptions.SubmissionSourceNotConnectedException"/> when the account is not connected,
/// <see cref="Exceptions.SubmissionSourceNotFoundException"/> for an unknown course or assignment,
/// and <see cref="Exceptions.SubmissionSourceUnavailableException"/> when the source fails.
/// </summary>
public interface ISubmissionSource
{
    Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);
}
