using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;

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

        var now = _timeProvider.GetUtcNow();
        job.Cancel(now);
        if (job.IsGroup)
        {
            var children = await _jobs.GetChildrenAsync(job.Id, cancellationToken);
            foreach (var child in children)
            {
                child.Cancel(now);
            }

            JobGroups.Track(job, children, now);
        }

        await _jobs.SaveChangesAsync(cancellationToken);
        return JobPayloads.ToResponse(job);
    }

    public async Task<GradeAllResponse> QueueGradeAssignmentAsync(
        string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default)
    {
        var plan = await _grading.PlanGradeAllAsync(courseId, assignmentId, regrade, cancellationToken);
        var (job, queued, alreadyQueued, problems) = await QueueGroupAsync(plan, JobType.GradeAssignment,
            JobPayloads.Write(new GradeAssignmentPayload(courseId, assignmentId, regrade)), $"grade-all:{assignmentId}", courseId, assignmentId,
            "Grading all students of this assignment is already running.", cancellationToken);
        return new GradeAllResponse(job, queued, plan.Students, plan.AlreadyDrafted, alreadyQueued, plan.NoWork, plan.NotFetched, problems);
    }

    public async Task<JobResponse> QueueGradeStudentAsync(
        string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var name = await _grading.FindStudentNameAsync(courseId, studentId, cancellationToken);
        var group = Job.Group(JobType.GradeStudent, JobPayloads.Write(new GradeStudentPayload(courseId, studentId, name, regrade)), now,
            dedupKey: $"grade-student:{courseId}:{studentId}", courseId: courseId);
        group.SetChildTotal(1);
        var active = await _jobs.AddOrGetActiveAsync(group, cancellationToken);
        if (active.Id != group.Id)
        {
            return JobPayloads.ToResponse(active);
        }

        var fetch = new Job(JobType.FetchStudentWork, JobPayloads.Write(new FetchStudentWorkPayload(courseId, studentId, group.Id, regrade)), now,
            parentId: group.Id, courseId: courseId, maxAttempts: MaxGradingAttempts);
        await _jobs.AddOrGetActiveAsync(fetch, cancellationToken);
        _signal.Notify();
        return JobPayloads.ToResponse(group);
    }

    public async Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
        Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
        bool regrade = false, CancellationToken cancellationToken = default)
    {
        var plan = await _grading.PlanGradeStudentAsync(courseId, studentId, regrade, cancellationToken);
        var problems = fetchProblems.Concat(plan.Problems).ToList();
        var parent = await _jobs.FindAsync(parentId, cancellationToken);
        if (parent is not { IsGroup: true, Status: JobStatus.Running, CancelRequested: false })
        {
            return new FetchStudentWorkResult(fetched, 0, plan.AlreadyDrafted, 0, plan.NoWork, plan.NotFetched, problems);
        }

        var now = _timeProvider.GetUtcNow();
        var existing = (await _jobs.GetChildrenAsync(parentId, cancellationToken)).Count;
        parent.SetChildTotal(existing + plan.VersionIds.Count);
        var (queued, alreadyQueued) = await AddGradingsAsync(parent, plan, courseId, null, now, cancellationToken);
        parent.SetChildTotal(existing + queued);
        await _jobs.SaveChangesAsync(cancellationToken);
        _signal.Notify();
        return new FetchStudentWorkResult(fetched, queued, plan.AlreadyDrafted, alreadyQueued, plan.NoWork, plan.NotFetched, problems);
    }

    private async Task<(JobResponse? Job, int Queued, int AlreadyQueued, IReadOnlyList<string> Problems)> QueueGroupAsync(
        GradeAllPlan plan, JobType type, string payload, string dedupKey, string courseId, string? assignmentId, string alreadyRunning,
        CancellationToken cancellationToken)
    {
        if (plan.VersionIds.Count == 0)
        {
            return (null, 0, 0, plan.Problems);
        }

        var now = _timeProvider.GetUtcNow();
        var group = Job.Group(type, payload, now, dedupKey: dedupKey, courseId: courseId, assignmentId: assignmentId);
        group.SetChildTotal(plan.VersionIds.Count);
        var active = await _jobs.AddOrGetActiveAsync(group, cancellationToken);
        if (active.Id != group.Id)
        {
            return (JobPayloads.ToResponse(active), 0, 0, [alreadyRunning]);
        }

        var (queued, alreadyQueued) = await AddGradingsAsync(group, plan, courseId, assignmentId, now, cancellationToken);
        group.SetChildTotal(queued);
        if (queued == 0)
        {
            JobGroups.Track(group, [], now);
        }

        await _jobs.SaveChangesAsync(cancellationToken);
        _signal.Notify();
        return (JobPayloads.ToResponse(group), queued, alreadyQueued, plan.Problems);
    }

    private async Task<(int Queued, int AlreadyQueued)> AddGradingsAsync(
        Job group, GradeAllPlan plan, string courseId, string? assignmentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var queued = 0;
        var alreadyQueued = 0;
        foreach (var versionId in plan.VersionIds)
        {
            var childAssignment = plan.AssignmentIds?.GetValueOrDefault(versionId) ?? assignmentId;
            var child = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload(versionId, PartOfGradeAll: true)), now,
                parentId: group.Id, dedupKey: $"grade:{versionId}", courseId: courseId, assignmentId: childAssignment,
                maxAttempts: MaxGradingAttempts);
            var added = await _jobs.AddOrGetActiveAsync(child, cancellationToken);
            if (added.Id == child.Id)
            {
                queued++;
            }
            else
            {
                alreadyQueued++;
            }
        }

        return (queued, alreadyQueued);
    }

    public async Task<JobResponse> QueueEvaluationRunAsync(
        Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var group = Job.Group(JobType.EvaluationRun, JobPayloads.Write(new EvaluationRunPayload(runId, courseId)), now,
            dedupKey: $"evaluation-run:{runId}", courseId: courseId);
        group.SetChildTotal(resultIds.Count);
        await _jobs.AddOrGetActiveAsync(group, cancellationToken);

        foreach (var resultId in resultIds)
        {
            var child = new Job(JobType.EvaluationGrade, JobPayloads.Write(new EvaluationGradePayload(resultId)), now,
                parentId: group.Id, dedupKey: $"evaluation-grade:{resultId}", courseId: courseId, maxAttempts: MaxGradingAttempts);
            await _jobs.AddOrGetActiveAsync(child, cancellationToken);
        }

        await _jobs.SaveChangesAsync(cancellationToken);
        _signal.Notify();
        return JobPayloads.ToResponse(group);
    }

    public async Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var job = new Job(JobType.AnalyzeMistakes, JobPayloads.Write(new AnalyzeMistakesPayload(courseId, assignmentId)),
            _timeProvider.GetUtcNow(), dedupKey: $"analyze-mistakes:{assignmentId}", courseId: courseId, assignmentId: assignmentId,
            maxAttempts: MaxGradingAttempts);
        var queued = await _jobs.AddOrGetActiveAsync(job, cancellationToken);
        _signal.Notify();
        return JobPayloads.ToResponse(queued);
    }

    public async Task<JobResponse> QueueAnalyzeStudentAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default)
    {
        var name = await _grading.FindStudentNameAsync(courseId, studentId, cancellationToken);
        var job = new Job(JobType.AnalyzeStudent, JobPayloads.Write(new AnalyzeStudentPayload(courseId, studentId, name, kind)),
            _timeProvider.GetUtcNow(), dedupKey: $"analyze-student:{kind}:{courseId}:{studentId}", courseId: courseId,
            maxAttempts: MaxGradingAttempts);
        var queued = await _jobs.AddOrGetActiveAsync(job, cancellationToken);
        _signal.Notify();
        return JobPayloads.ToResponse(queued);
    }

    public async Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var children = await _jobs.GetChildrenAsync(jobId, cancellationToken);
        return await DescribeAsync(children, cancellationToken);
    }

    public async Task<JobOverviewResponse> GetOverviewAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default)
    {
        var since = _timeProvider.GetUtcNow() - CountWindow;
        var counts = await _jobs.CountByStatusAsync(since, type, cancellationToken);
        var jobs = await _jobs.GetRecentAsync(status, type, courseId, assignmentId, RecentJobsOverview, cancellationToken);

        var items = await DescribeAsync(jobs, cancellationToken);

        return new JobOverviewResponse(
            new JobCounts(Count(JobStatus.Queued), Count(JobStatus.Running), Count(JobStatus.Succeeded), Count(JobStatus.Failed),
                Count(JobStatus.Canceled), since),
            items);

        int Count(JobStatus key)
        {
            return counts.GetValueOrDefault(key);
        }
    }

    private async Task<IReadOnlyList<JobOverviewItem>> DescribeAsync(IReadOnlyList<Job> jobs, CancellationToken cancellationToken)
    {
        var responses = jobs.Select(JobPayloads.ToResponse).ToList();
        var versionIds = responses.Where(j => j.VersionId is not null).Select(j => j.VersionId!.Value).Distinct().ToList();
        var subjects = (await _jobs.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);
        var groupIds = jobs.Where(j => j.IsGroup).Select(j => j.Id).ToList();
        var childCounts = (await _jobs.CountChildrenAsync(groupIds, cancellationToken)).ToLookup(c => c.ParentId);
        var firstChildVersions = await ChildVersionsAsync(groupIds, cancellationToken);
        var childSubjects = (await _jobs.GetSubjectsAsync(firstChildVersions.Values.Distinct().ToList(), cancellationToken))
            .ToDictionary(s => s.VersionId);
        var groupSubjects = firstChildVersions
            .Where(pair => childSubjects.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => childSubjects[pair.Value]);

        return jobs.Zip(responses)
            .Select(pair =>
            {
                var (job, response) = pair;
                var subject = response.VersionId is { } versionId ? subjects.GetValueOrDefault(versionId) : null;
                JobChildCounts? children = null;
                if (job.IsGroup)
                {
                    var counts = childCounts[job.Id].ToDictionary(c => c.Status, c => c.Count);
                    children = new JobChildCounts(counts.Values.Sum(), counts.GetValueOrDefault(JobStatus.Queued),
                        counts.GetValueOrDefault(JobStatus.Running), counts.GetValueOrDefault(JobStatus.Succeeded),
                        counts.GetValueOrDefault(JobStatus.Failed), counts.GetValueOrDefault(JobStatus.Canceled));
                }

                var groupSubject = job.IsGroup ? groupSubjects.GetValueOrDefault(job.Id) : null;
                if (job.Type == JobType.GradeStudent)
                {
                    var student = JobPayloads.Read<GradeStudentPayload>(job.Payload);
                    return new JobOverviewItem(response, job.CourseId, null, null, null, student.StudentId,
                        student.StudentName ?? groupSubject?.StudentName, null, children);
                }

                if (job.Type == JobType.AnalyzeStudent)
                {
                    var analyzed = JobPayloads.Read<AnalyzeStudentPayload>(job.Payload);
                    return new JobOverviewItem(response, job.CourseId, null, null, null, analyzed.StudentId, analyzed.StudentName, null,
                        null);
                }

                var assignmentId = subject?.AssignmentId ?? job.AssignmentId;
                var title = subject?.AssignmentTitle ?? groupSubject?.AssignmentTitle;
                return new JobOverviewItem(response, subject?.CourseId ?? job.CourseId, assignmentId, title, subject?.SubmissionId,
                    subject?.StudentId, subject?.StudentName, subject?.VersionNumber, children);
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> ChildVersionsAsync(IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
    {
        var versionIds = new Dictionary<Guid, Guid>();
        foreach (var groupId in groupIds)
        {
            var first = (await _jobs.GetChildrenAsync(groupId, cancellationToken))
                .Select(child => JobPayloads.ToResponse(child).VersionId)
                .FirstOrDefault(versionId => versionId is not null);
            if (first is { } versionId)
            {
                versionIds[groupId] = versionId;
            }
        }

        return versionIds;
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

        if (job.Type == JobType.GradeAssignment)
        {
            var payload = JobPayloads.Read<GradeAssignmentPayload>(job.Payload);
            var again = await QueueGradeAssignmentAsync(payload.CourseId, payload.AssignmentId, payload.Regrade, cancellationToken);
            return again.Job ?? throw new InvalidRequestException("Nothing left to grade: every student already has a draft.");
        }

        if (job.Type == JobType.GradeStudent)
        {
            var payload = JobPayloads.Read<GradeStudentPayload>(job.Payload);
            return await QueueGradeStudentAsync(payload.CourseId, payload.StudentId, payload.Regrade, cancellationToken);
        }

        if (job.Type == JobType.AnalyzeStudent)
        {
            var payload = JobPayloads.Read<AnalyzeStudentPayload>(job.Payload);
            return await QueueAnalyzeStudentAsync(payload.CourseId, payload.StudentId, payload.Kind, cancellationToken);
        }

        if (job.Type == JobType.EvaluationGrade)
        {
            var again = new Job(JobType.EvaluationGrade, job.Payload, _timeProvider.GetUtcNow(), courseId: job.CourseId,
                maxAttempts: MaxGradingAttempts);
            var queued = await _jobs.AddOrGetActiveAsync(again, cancellationToken);
            await _jobs.SaveChangesAsync(cancellationToken);
            _signal.Notify();
            return JobPayloads.ToResponse(queued);
        }

        if (job.Type == JobType.EvaluationRun)
        {
            throw new InvalidRequestException("Start a new evaluation run on the Evaluation page; it grades every approved report again.");
        }

        if (job.Type == JobType.FetchStudentWork)
        {
            throw new InvalidRequestException("Retry the Grade student job this step belongs to; it fetches again and then grades.");
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
