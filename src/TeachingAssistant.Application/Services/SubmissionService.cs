using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class SubmissionService : ISubmissionService
{
    private readonly ISubmissionSource _submissionSource;

    public SubmissionService(ISubmissionSource submissionSource)
    {
        _submissionSource = submissionSource;
    }

    public async Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var submissions = await _submissionSource.GetSubmissionsAsync(courseId, assignmentId, cancellationToken);
        return submissions.OrderBy(s => s.StudentName).ToList();
    }
}
