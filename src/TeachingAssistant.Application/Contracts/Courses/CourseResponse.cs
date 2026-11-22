namespace TeachingAssistant.Application.Contracts;

public record CourseResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Section { get; init; }
    public required string State { get; init; }
    public string? Link { get; init; }
}
