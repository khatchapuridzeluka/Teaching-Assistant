namespace TeachingAssistant.Application.Models;

public record AgentRunHooks
{
    public required Func<AgentEntry, Task> OnStep { get; init; }
    public required Func<bool> ShouldStop { get; init; }
}
