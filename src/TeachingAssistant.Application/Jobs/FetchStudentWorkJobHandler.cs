using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

public class FetchStudentWorkJobHandler : IJobHandler
{
    private readonly ISubmissionStore _submissions;
    private readonly ISubmissionFetchService _fetch;
    private readonly IJobService _jobs;

    public FetchStudentWorkJobHandler(ISubmissionStore submissions, ISubmissionFetchService fetch, IJobService jobs)
    {
        _submissions = submissions;
        _fetch = fetch;
        _jobs = jobs;
    }

    public JobType Type => JobType.FetchStudentWork;

    public async Task<string?> RunAsync(string payload, CancellationToken cancellationToken)
    {
        var input = JobPayloads.Read<FetchStudentWorkPayload>(payload);
        var turnedIn = (await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, input.CourseId, cancellationToken))
            .Where(s => s.StudentId == input.StudentId && s.IsTurnedIn)
            .ToList();

        var fetched = 0;
        var problems = new List<string>();
        foreach (var submission in turnedIn)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var summary = await _fetch.FetchAsync(input.CourseId, submission.AssignmentId, cancellationToken);
                fetched++;
                problems.AddRange(summary.Problems
                    .Where(p => p.StudentName == submission.StudentName)
                    .Select(p => $"Fetching: {p.Detail}"));
            }
            catch (InvalidRequestException ex)
            {
                problems.Add($"Fetching an assignment failed: {ex.Message}");
            }
        }

        var result = await _jobs.QueueStudentGradingsAsync(input.ParentId, input.CourseId, input.StudentId, fetched, problems,
            cancellationToken);
        return JobPayloads.Write(result);
    }
}
