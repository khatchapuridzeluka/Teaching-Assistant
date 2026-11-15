using System.Collections.Frozen;

namespace TeachingAssistant.Domain.Ingestion;

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

    /// <summary>Kinds whose code is extracted into the version's code folder: archives, repositories and single source files.</summary>
    public static bool IsCode(ContentKind kind) => kind is ContentKind.CodeArchive or ContentKind.Repository or ContentKind.SourceFile;

    /// <summary>Kinds stored as an archive to unpack: an uploaded archive or a downloaded repository.</summary>
    public static bool IsArchive(ContentKind kind) => kind is ContentKind.CodeArchive or ContentKind.Repository;

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
