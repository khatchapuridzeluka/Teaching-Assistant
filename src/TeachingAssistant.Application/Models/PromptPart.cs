namespace TeachingAssistant.Application.Models;

/// <summary>
/// One piece of a prompt given to the model, in order: text, or a file the model reads itself (a PDF or an image).
/// Order matters: parts shared by every student (instructions) come first, so they can be cached.
/// </summary>
public abstract record PromptPart;

/// <summary>Plain text, e.g. the task statement or a student's source code.</summary>
public sealed record TextPart(string Text) : PromptPart;

/// <summary>
/// A file the model reads directly. MediaType must be one of <see cref="Supported"/>; check with <see cref="IsSupported"/>.
/// </summary>
public sealed record FilePart(string Name, string MediaType, byte[] Content) : PromptPart
{
    /// <summary>File types the model can read: PDF documents and common images.</summary>
    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/png", "image/jpeg", "image/gif", "image/webp" };

    public static bool IsSupported(string? mediaType) => mediaType is not null && Supported.Contains(mediaType);
}
