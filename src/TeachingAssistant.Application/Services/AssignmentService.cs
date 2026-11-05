using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

// Pass-through for now; later it adds per-assignment grading status counts from our own database.
public class AssignmentService : IAssignmentService
{
    private readonly ISubmissionSource _submissionSource;

    public AssignmentService(ISubmissionSource submissionSource)
    {
        _submissionSource = submissionSource;
    }

    public async Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var assignments = await _submissionSource.GetAssignmentsAsync(courseId, cancellationToken);

        // Newest first, the order the teacher works in.
        return assignments.OrderByDescending(a => a.CreatedAt).ToList();
    }
}
