using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Models;

public record AgentRunHooks
{
    public required Func<AgentEntry, Task> OnStep { get; init; }
    public required Func<bool> ShouldStop { get; init; }
}

public class AgentRunContext
{
    private readonly Queue<Guid> _queuedJobs = new();
    private readonly List<AgentLink> _links = [];
    private readonly List<string> _replies = [];

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
        _links.Clear();
        _replies.Clear();
    }

    public void RecordLinks(IEnumerable<AgentLink> links)
    {
        _links.AddRange(links);
    }

    public IReadOnlyList<AgentLink> TakeLinks()
    {
        var links = _links.ToList();
        _links.Clear();
        return links;
    }

    public void RecordReplies(IEnumerable<string> replies)
    {
        _replies.Clear();
        _replies.AddRange(replies);
    }

    public string[] TakeReplies()
    {
        var replies = _replies.ToArray();
        _replies.Clear();
        return replies;
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
