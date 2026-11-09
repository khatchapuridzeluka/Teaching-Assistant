using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobService : IJobService
{
    private const int MaxGradingAttempts = 3;
    private const int RecentJobsPerAssignment = 100;
    private const int RecentJobsOverview = 200;
    private static readonly TimeSpan CountWindow = TimeSpan.FromHours(24);

    private readonly IJobStore _jobs;
    private readonly IGradingService _grading;
    private readonly IJobSignal _signal;
    private readonly TimeProvider _timeProvider;

    public JobService(IJobStore jobs, IGradingService grading, IJobSignal signal, TimeProvider timeProvider)
    {
        _jobs = jobs;
        _grading = grading;
        _signal = signal;
        _timeProvider = timeProvider;
    }

    public async Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var gradable = await _grading.CheckAsync(versionId, cancellationToken);
        if (gradable is null)
        {
            return null;
        }

        var job = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload(versionId)), _timeProvider.GetUtcNow(),
            dedupKey: $"grade:{versionId}", courseId: gradable.CourseId, assignmentId: gradable.AssignmentId,
            maxAttempts: MaxGradingAttempts);
        var queued = await _jobs.AddOrGetActiveAsync(job, cancellationToken);
        _signal.Notify();
        return JobPayloads.ToResponse(queued);
    }

    public async Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        return job is null ? null : JobPayloads.ToResponse(job);
    }

    public async Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        var jobs = await _jobs.GetByAssignmentAsync(assignmentId, RecentJobsPerAssignment, cancellationToken);
        return jobs.Select(JobPayloads.ToResponse).ToList();
    }

    public async Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.Cancel(_timeProvider.GetUtcNow());
        await _jobs.SaveChangesAsync(cancellationToken);
        return JobPayloads.ToResponse(job);
    }

    public async Task<JobOverviewResponse> GetOverviewAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default)
    {
        var since = _timeProvider.GetUtcNow() - CountWindow;
        var counts = await _jobs.CountByStatusAsync(since, type, cancellationToken);
        var jobs = await _jobs.GetRecentAsync(status, type, courseId, assignmentId, RecentJobsOverview, cancellationToken);

        var responses = jobs.Select(JobPayloads.ToResponse).ToList();
        var versionIds = responses.Where(j => j.VersionId is not null).Select(j => j.VersionId!.Value).Distinct().ToList();
        var subjects = (await _jobs.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);

        var items = jobs.Zip(responses)
            .Select(pair =>
            {
                var (job, response) = pair;
                var subject = response.VersionId is { } versionId ? subjects.GetValueOrDefault(versionId) : null;
                return new JobOverviewItem(response, subject?.CourseId ?? job.CourseId, subject?.AssignmentId ?? job.AssignmentId,
                    subject?.AssignmentTitle, subject?.SubmissionId, subject?.StudentId, subject?.StudentName, subject?.VersionNumber);
            })
            .ToList();

        return new JobOverviewResponse(
            new JobCounts(Count(JobStatus.Queued), Count(JobStatus.Running), Count(JobStatus.Succeeded), Count(JobStatus.Failed),
                Count(JobStatus.Canceled), since),
            items);

        int Count(JobStatus key)
        {
            return counts.GetValueOrDefault(key);
        }
    }

    public async Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status is not (JobStatus.Failed or JobStatus.Canceled))
        {
            throw new InvalidRequestException($"Only a failed or canceled job can be retried; this one is {job.Status}.");
        }

        if (job.Type != JobType.GradeVersion)
        {
            throw new InvalidRequestException($"Retrying {job.Type} jobs is not supported yet.");
        }

        var versionId = JobPayloads.Read<GradeVersionPayload>(job.Payload).VersionId;
        return await QueueGradeVersionAsync(versionId, cancellationToken)
               ?? throw new InvalidRequestException("The version this job graded no longer exists.");
    }
}
