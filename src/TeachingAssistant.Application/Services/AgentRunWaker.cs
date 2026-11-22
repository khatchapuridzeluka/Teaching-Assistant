using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Services;

public class AgentRunWaker : IAgentRunWaker
{
    private readonly IAgentChatStore _chats;
    private readonly IJobStore _jobs;
    private readonly IJobSignal _signal;
    private readonly JobReporter _reporter;
    private readonly TimeProvider _timeProvider;

    public AgentRunWaker(IAgentChatStore chats, IJobStore jobs, IJobSignal signal, JobReporter reporter, TimeProvider timeProvider)
    {
        _chats = chats;
        _jobs = jobs;
        _signal = signal;
        _reporter = reporter;
        _timeProvider = timeProvider;
    }

    public async Task OnJobFinishedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.FindAsync(jobId, cancellationToken);
        if (job is not { IsFinished: true })
        {
            return;
        }

        await SettleAsync(job, cancellationToken);
        await WakeWaitingOnAsync(job.Id, cancellationToken);

        if (job.ParentId is { } parentId && await _jobs.FindAsync(parentId, cancellationToken) is { IsFinished: true } parent)
        {
            await WakeWaitingOnAsync(parent.Id, cancellationToken);
        }
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var woken = 0;
        foreach (var run in await _chats.GetUnfinishedRunsAsync(cancellationToken))
        {
            if (run.Status == AgentRunStatus.Waiting)
            {
                woken += await TryWakeAsync(run, cancellationToken) ? 1 : 0;
            }
            else if (run.JobId is { } jobId && await _jobs.FindAsync(jobId, cancellationToken) is { IsFinished: true } job)
            {
                await SettleAsync(job, cancellationToken);
            }
        }

        return woken;
    }

    private async Task SettleAsync(Job job, CancellationToken cancellationToken)
    {
        if (job.Type != JobType.AgentRun)
        {
            return;
        }

        var run = await _chats.FindRunAsync(JobPayloads.Read<AgentRunPayload>(job.Payload).RunId, cancellationToken);
        if (run is null || run.IsFinished || run.JobId != job.Id)
        {
            return;
        }

        if (run.Status == AgentRunStatus.Waiting)
        {
            await TryWakeAsync(run, cancellationToken);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        if (job.Status == JobStatus.Canceled)
        {
            run.Cancel(now);
        }
        else
        {
            run.Fail(job.Error ?? "The assistant stopped before finishing this message.", now);
        }

        await _chats.SaveChangesAsync(cancellationToken);
    }

    private async Task WakeWaitingOnAsync(Guid jobId, CancellationToken cancellationToken)
    {
        foreach (var run in await _chats.GetRunsWaitingOnAsync(jobId, cancellationToken))
        {
            await TryWakeAsync(run, cancellationToken);
        }
    }

    private async Task<bool> TryWakeAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var jobs = new List<Job>();
        foreach (var jobId in run.WaitingJobIds)
        {
            if (await _jobs.FindAsync(jobId, cancellationToken) is { } waited)
            {
                jobs.Add(waited);
            }
        }

        var allFinished = jobs.All(j => j.IsFinished);
        if (!allFinished && !run.IsWaitOver(now))
        {
            return false;
        }

        var conversation = await _chats.FindConversationAsync(run.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent run {run.Id} has no conversation.");
        var results = await _reporter.DescribeAsync(jobs, cancellationToken);
        var text = allFinished
            ? $"The background work you waited for has finished:\n{results}"
            : $"Not everything has finished after {(int)AgentRun.WaitLimit.TotalMinutes} minutes:\n{results}\n"
              + "You can wait again for the jobs still in progress, or tell the teacher where things stand.";

        var links = await _reporter.LinksAsync(jobs, cancellationToken);
        _chats.Add(conversation.AddNotice(run.Id, text, null, now, AgentLinks.Write(links)));
        run.Resume();

        var job = new Job(JobType.AgentRun, JobPayloads.Write(new AgentRunPayload { RunId = run.Id }), now, courseId: run.CourseId,
            assignmentId: run.AssignmentId, maxAttempts: 1);
        await _jobs.AddOrGetActiveAsync(job, cancellationToken);
        run.AttachJob(job.Id);
        await _chats.SaveChangesAsync(cancellationToken);
        _signal.Notify();
        return true;
    }
}
