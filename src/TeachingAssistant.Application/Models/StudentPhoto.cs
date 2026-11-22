namespace TeachingAssistant.Application.Models;

public record StudentPhoto
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string Hash { get; init; }
}
