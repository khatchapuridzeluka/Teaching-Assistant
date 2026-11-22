namespace TeachingAssistant.Application.Models;

public record ReadDocument
{
    public required string Text { get; init; }
    public required IReadOnlyList<DocumentImage> Images { get; init; }
}

public record DocumentImage
{
    public required string Name { get; init; }
    public required string MediaType { get; init; }
    public required byte[] Content { get; init; }
}
