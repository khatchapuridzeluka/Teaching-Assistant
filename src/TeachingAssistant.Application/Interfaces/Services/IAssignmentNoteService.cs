using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IAssignmentNoteService
{
    Task<IReadOnlyList<AssignmentNoteResponse>> GetAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Adds a note after the assignment's existing ones.</summary>
    Task<AssignmentNoteResponse> AddAsync(
        string courseId, string assignmentId, AssignmentNoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a note; null when it does not exist or is deleted.</summary>
    Task<AssignmentNoteResponse?> UpdateAsync(Guid id, AssignmentNoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marks a note deleted; false when it does not exist or is already deleted.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
