using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface ISubmissionSource
{
    Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default);

    Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionResponse>> GetCourseSubmissionsAsync(string courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RosterStudent>> GetStudentsAsync(string courseId, CancellationToken cancellationToken = default);
}
