namespace TeachingAssistant.Application.Models;

public record DocumentImage
{
    public required string Name { get; init; }
    public required string MediaType { get; init; }
    public required byte[] Content { get; init; }
}
