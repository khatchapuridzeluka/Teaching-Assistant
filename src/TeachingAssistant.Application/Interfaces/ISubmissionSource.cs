using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

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

    Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default);

    Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Every student's submission to every assignment of a course, with grades (no files).</summary>
    Task<IReadOnlyList<SubmissionResponse>> GetCourseSubmissionsAsync(string courseId, CancellationToken cancellationToken = default);

    /// <summary>The students on a course's roster now.</summary>
    Task<IReadOnlyList<RosterStudent>> GetStudentsAsync(string courseId, CancellationToken cancellationToken = default);
}
