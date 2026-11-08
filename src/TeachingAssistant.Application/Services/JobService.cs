using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class JobService : IJobService
{
    private const int MaxGradingAttempts = 3;
    private const int RecentJobsPerAssignment = 100;

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
}
