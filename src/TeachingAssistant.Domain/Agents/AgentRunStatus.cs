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
