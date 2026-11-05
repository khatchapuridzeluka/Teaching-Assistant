using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Assignments of a course, for the Home and Assignment screens.
/// </summary>
public interface IAssignmentService
{
    Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default);
}
