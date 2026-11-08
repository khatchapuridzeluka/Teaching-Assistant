using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

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

        return assignments.OrderByDescending(a => a.CreatedAt).ToList();
    }
}
