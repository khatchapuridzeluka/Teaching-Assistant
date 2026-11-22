namespace TeachingAssistant.Application.Models;

public record ReadDocument
{
    public required string Text { get; init; }
    public required IReadOnlyList<DocumentImage> Images { get; init; }
}
