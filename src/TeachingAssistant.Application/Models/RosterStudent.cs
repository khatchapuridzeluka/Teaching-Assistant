namespace TeachingAssistant.Application.Models;

public record RosterStudent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? PhotoUrl { get; init; }
}
