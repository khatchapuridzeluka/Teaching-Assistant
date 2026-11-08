namespace TeachingAssistant.Application.Models;

public abstract record PromptPart;

public sealed record TextPart(string Text) : PromptPart;

public sealed record FilePart(string Name, string MediaType, byte[] Content) : PromptPart
{
    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp" };

    public static bool IsSupported(string? mediaType) => mediaType is not null && Supported.Contains(mediaType);
}
