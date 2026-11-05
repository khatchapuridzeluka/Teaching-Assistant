using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

/// <summary>A stored original to extract code from: an archive to unpack or a source file to copy.</summary>
public record ExtractableFile(string StoredName, ContentKind Kind);

/// <summary>
/// The result of extracting a version's code into code/.
/// </summary>
/// <param name="ContentHash">SHA-256 over the extracted files' paths and contents; null when nothing was extracted.</param>
/// <param name="FileCount">Number of files in code/.</param>
/// <param name="ExtractedStoredNames">Originals whose code was extracted successfully.</param>
/// <param name="Problems">What could not be extracted, one sentence each.</param>
public record CodeExtraction(
    string? ContentHash,
    int FileCount,
    IReadOnlyList<string> ExtractedStoredNames,
    IReadOnlyList<string> Problems);

/// <summary>One extracted file in a version's code/ folder. Path uses forward slashes.</summary>
public record CodeFile(string Path, long SizeBytes);
