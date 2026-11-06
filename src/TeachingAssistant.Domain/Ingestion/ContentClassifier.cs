using System.Collections.Frozen;

namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// Decides what a submitted file is for grading, from its name and type. The extension decides first because
/// sources report the same format under different types (Drive says application/x-zip-compressed for most zips).
/// </summary>
public static class ContentClassifier
{
    private static readonly FrozenSet<string> ArchiveExtensions =
        new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> SourceExtensions =
        new[] { ".java", ".kt", ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".cs", ".py", ".js", ".ts", ".sql" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> DocumentExtensions =
        new[] { ".pdf", ".docx", ".doc", ".odt", ".rtf", ".txt", ".md", ".pptx", ".xlsx" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ImageExtensions =
        new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ArchiveMimeTypes =
        new[]
        {
            "application/zip", "application/x-zip-compressed", "application/x-rar", "application/vnd.rar",
            "application/x-rar-compressed", "application/x-7z-compressed", "application/x-tar", "application/gzip",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static ContentKind Classify(string fileName, string? mimeType)
    {
        var extension = Path.GetExtension(fileName);

        if (ArchiveExtensions.Contains(extension))
        {
            return ContentKind.CodeArchive;
        }

        if (SourceExtensions.Contains(extension))
        {
            return ContentKind.SourceFile;
        }

        if (DocumentExtensions.Contains(extension))
        {
            return ContentKind.Document;
        }

        if (ImageExtensions.Contains(extension))
        {
            return ContentKind.Image;
        }

        // No useful extension: fall back to the reported type.
        if (mimeType is null)
        {
            return ContentKind.Other;
        }

        if (ArchiveMimeTypes.Contains(mimeType))
        {
            return ContentKind.CodeArchive;
        }

        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ContentKind.Image;
        }

        if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return ContentKind.Document;
        }

        return ContentKind.Other;
    }
}
