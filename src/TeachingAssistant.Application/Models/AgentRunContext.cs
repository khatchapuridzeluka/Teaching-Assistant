namespace TeachingAssistant.Application.Models;

public record AgentRunHooks(Func<AgentEntry, Task> OnStep, Func<bool> ShouldStop);

public class AgentRunContext
{
    private readonly Queue<Guid> _queuedJobs = new();

    public Guid? RunId { get; private set; }

    public IReadOnlyList<Guid>? WaitFor { get; private set; }

    public bool StepLimitReached { get; private set; }

    public bool StopRequested => WaitFor is not null || StepLimitReached;

    public void Begin(Guid runId)
    {
        RunId = runId;
        WaitFor = null;
        StepLimitReached = false;
        _queuedJobs.Clear();
    }

    public void RecordQueuedJob(Guid jobId)
    {
        _queuedJobs.Enqueue(jobId);
    }

    public Guid? TakeQueuedJob()
    {
        return _queuedJobs.TryDequeue(out var jobId) ? jobId : null;
    }

    public void RequestWait(IReadOnlyList<Guid> jobIds)
    {
        WaitFor = (WaitFor ?? []).Concat(jobIds).Distinct().ToList();
    }

    public void ReachStepLimit()
    {
        StepLimitReached = true;
    }
}
