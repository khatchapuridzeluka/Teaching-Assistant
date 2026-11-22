namespace TeachingAssistant.Application.Models;

public record AgentToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}
