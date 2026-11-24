namespace TeachingAssistant.Application.Contracts;

public record ReferenceFile
{
    public required string Name { get; init; }
    public required string Content { get; init; }
}
