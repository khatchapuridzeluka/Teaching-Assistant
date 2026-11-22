namespace TeachingAssistant.Application.Models;

public abstract record PromptPart;

public sealed record TextPart : PromptPart
{
    public required string Text { get; init; }
}

public sealed record FilePart : PromptPart
{
    public required string Name { get; init; }
    public required string MediaType { get; init; }
    public required byte[] Content { get; init; }

    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp" };

    public static bool IsSupported(string? mediaType) => mediaType is not null && Supported.Contains(mediaType);
}
