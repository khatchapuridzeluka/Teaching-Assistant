namespace TeachingAssistant.Domain.Agents;

public enum AgentRunStatus
{
    Queued,

    Running,

    Waiting,

    Succeeded,

    Failed,

    Canceled,
}

public class AgentRun
{
    public const int DefaultMaxSteps = 25;

    private AgentRun()
    {
    }

    public AgentRun(
        Guid conversationId, DateTimeOffset createdAt, string? page = null, string? courseId = null, string? assignmentId = null,
        int maxSteps = DefaultMaxSteps)
    {
        if (maxSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "A run needs at least one step.");
        }

        Id = Guid.CreateVersion7();
        ConversationId = conversationId;
        Status = AgentRunStatus.Queued;
        Page = page;
        CourseId = courseId;
        AssignmentId = assignmentId;
        MaxSteps = maxSteps;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public AgentRunStatus Status { get; private set; }

    public Guid? WaitingJobId { get; private set; }

    public int Steps { get; private set; }

    public int MaxSteps { get; private set; }

    public string? Reply { get; private set; }

    public string? Error { get; private set; }

    public string? Page { get; private set; }

    public string? CourseId { get; private set; }

    public string? AssignmentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public bool IsFinished => Status is AgentRunStatus.Succeeded or AgentRunStatus.Failed or AgentRunStatus.Canceled;

    public void Start(DateTimeOffset now)
    {
        Require(AgentRunStatus.Queued, "start");
        Status = AgentRunStatus.Running;
        StartedAt ??= now.ToUniversalTime();
    }

    public bool TryTakeStep()
    {
        Require(AgentRunStatus.Running, "take a step");
        if (Steps >= MaxSteps)
        {
            return false;
        }

        Steps++;
        return true;
    }

    public void WaitFor(Guid jobId)
    {
        Require(AgentRunStatus.Running, "wait for a job");
        Status = AgentRunStatus.Waiting;
        WaitingJobId = jobId;
    }

    public bool Resume(Guid finishedJobId)
    {
        if (Status != AgentRunStatus.Waiting || WaitingJobId != finishedJobId)
        {
            return false;
        }

        Status = AgentRunStatus.Queued;
        WaitingJobId = null;
        return true;
    }

    public void Succeed(string reply, DateTimeOffset now)
    {
        Require(AgentRunStatus.Running, "succeed");
        Status = AgentRunStatus.Succeeded;
        Reply = reply.Trim();
        FinishedAt = now.ToUniversalTime();
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Finish(AgentRunStatus.Failed, "fail", now);
        Error = error;
    }

    public void Cancel(DateTimeOffset now)
    {
        Finish(AgentRunStatus.Canceled, "be canceled", now);
    }

    private void Finish(AgentRunStatus status, string action, DateTimeOffset now)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException($"Agent run {Id} cannot {action}: it is already {Status}.");
        }

        Status = status;
        WaitingJobId = null;
        FinishedAt = now.ToUniversalTime();
    }

    private void Require(AgentRunStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Agent run {Id} cannot {action}: it is {Status}.");
        }
    }
}
