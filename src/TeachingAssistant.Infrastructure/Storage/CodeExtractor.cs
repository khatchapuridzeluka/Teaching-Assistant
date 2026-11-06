using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Readers;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Storage;

/// <summary>
/// Builds a version's code/ folder from its originals: archives (zip, rar, 7z, tar, gz) unpacked, source files copied,
/// junk left out. Student archives are untrusted, so entries can never be written outside the folder and the total
/// size and file count are limited by the bytes actually written, not by what the archive claims.
/// </summary>
public static class CodeExtractor
{
    private const int MaxFiles = 5_000;
    private const long MaxTotalBytes = 200L * 1024 * 1024;
    private const long MaxFileBytes = 25L * 1024 * 1024;

    private static readonly FrozenSet<string> ArchiveExtensions =
        new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // IDE settings, version control, build output and OS files: never part of what the student wrote.
    private static readonly FrozenSet<string> JunkFolders =
        new[] { "__MACOSX", ".idea", ".vscode", ".vs", ".git", ".gradle", ".settings", ".metadata", "out", "bin", "obj", "target", "build", "node_modules" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> JunkFiles =
        new[] { ".DS_Store", "Thumbs.db", "desktop.ini" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> JunkExtensions =
        new[] { ".class", ".jar", ".iml", ".exe", ".dll", ".o", ".obj", ".pdb" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Extracts into <paramref name="codeDirectory"/>, which is created if needed and should be empty.</summary>
    public static CodeExtraction Extract(
        string originalDirectory, string codeDirectory, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(codeDirectory);
        var budget = new Budget();
        var extracted = new List<string>();
        var problems = new List<string>();

        var codeFiles = files.Where(f => f.Kind is ContentKind.CodeArchive or ContentKind.SourceFile).ToList();
        var archiveCount = codeFiles.Count(f => f.Kind == ContentKind.CodeArchive);

        foreach (var file in codeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(originalDirectory, file.StoredName);
            var checkpoint = budget.Checkpoint();

            try
            {
                if (file.Kind == ContentKind.SourceFile)
                {
                    using var input = File.OpenRead(source);
                    WriteFile(input, Path.Combine(codeDirectory, file.StoredName), budget);
                }
                else
                {
                    // With several archives each gets its own folder, so equally named files cannot overwrite each other.
                    var target = archiveCount > 1
                        ? Path.Combine(codeDirectory, SafeSegment(Path.GetFileNameWithoutExtension(file.StoredName)))
                        : codeDirectory;
                    ExtractArchive(source, target, budget, cancellationToken);
                    ExtractNestedArchives(target, budget, problems, cancellationToken);
                }

                extracted.Add(file.StoredName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Remove what this file wrote before failing, so a half-extracted archive never counts as code.
                budget.RollBackTo(checkpoint);
                problems.Add($"Could not extract '{file.StoredName}': {Reason(ex)}");
            }
        }

        var hash = ComputeHash(codeDirectory, out var fileCount);
        if (fileCount == 0 && codeFiles.Count > 0 && problems.Count == 0)
        {
            problems.Add("No code files were found in the submitted archives.");
        }

        return new CodeExtraction(hash, fileCount, extracted, problems);
    }

    /// <summary>Lists code/ as forward-slash relative paths, sorted.</summary>
    public static IReadOnlyList<CodeFile> List(string codeDirectory) =>
        Directory.Exists(codeDirectory)
            ? Directory.EnumerateFiles(codeDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new CodeFile(RelativePath(codeDirectory, path), new FileInfo(path).Length))
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .ToList()
            : [];

    private static void ExtractArchive(string archivePath, string target, Budget budget, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(target);
        Directory.CreateDirectory(root);

        using var archive = ArchiveFactory.Open(archivePath, new ReaderOptions());
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                continue;
            }

            if (entry.IsEncrypted)
            {
                throw new InvalidDataException("the archive is password-protected.");
            }

            // A single-file .gz has no name inside; use the archive's own name without .gz.
            var relative = SafeRelativePath(entry.Key ?? Path.GetFileNameWithoutExtension(archivePath));
            if (relative is null || IsJunk(relative))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue; // would land outside the folder ("zip slip")
            }

            using var input = entry.OpenEntryStream();
            WriteFile(input, destination, budget);
        }
    }

    // Students sometimes zip a zip. Unpack archives found inside one level deep, next to where they were.
    private static void ExtractNestedArchives(string directory, Budget budget, List<string> problems, CancellationToken cancellationToken)
    {
        var nested = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => ArchiveExtensions.Contains(Path.GetExtension(path)))
            .ToList();

        foreach (var path in nested)
        {
            var checkpoint = budget.Checkpoint();
            try
            {
                var target = Path.Combine(Path.GetDirectoryName(path)!, SafeSegment(Path.GetFileNameWithoutExtension(path)));
                ExtractArchive(path, target, budget, cancellationToken);
                File.Delete(path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                budget.RollBackTo(checkpoint); // the nested archive itself stays, so nothing is lost
                problems.Add($"Could not extract nested archive '{Path.GetFileName(path)}': {Reason(ex)}");
            }
        }
    }

    private static void WriteFile(Stream input, string destination, Budget budget)
    {
        budget.AddFile(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write);
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            written += read;
            if (written > MaxFileBytes)
            {
                throw new InvalidDataException($"a file is larger than {MaxFileBytes / 1024 / 1024} MB.");
            }

            budget.AddBytes(read);
            output.Write(buffer, 0, read);
        }
    }

    // Splits an entry name into safe folder and file names; null for names that try to climb out ("..").
    private static string? SafeRelativePath(string entryName)
    {
        var segments = entryName.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s != ".")
            .ToList();
        if (segments.Count == 0 || segments.Contains(".."))
        {
            return null;
        }

        return Path.Combine(segments.Select(SafeSegment).ToArray());
    }

    private static string SafeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(segment.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "_" : cleaned;
    }

    private static bool IsJunk(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar);
        var fileName = parts[^1];
        return parts[..^1].Any(JunkFolders.Contains)
               || JunkFiles.Contains(fileName)
               || fileName.StartsWith("._", StringComparison.Ordinal) // macOS resource forks
               || JunkExtensions.Contains(Path.GetExtension(fileName));
    }

    // One hash for the whole folder: same paths and same contents give the same hash, however the files were zipped.
    private static string? ComputeHash(string codeDirectory, out int fileCount)
    {
        var lines = new StringBuilder();
        var files = List(codeDirectory);
        foreach (var file in files)
        {
            using var stream = File.OpenRead(Path.Combine(codeDirectory, file.Path));
            lines.Append(file.Path).Append('\0').Append(Convert.ToHexStringLower(SHA256.HashData(stream))).Append('\n');
        }

        fileCount = files.Count;
        return fileCount == 0 ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString())));
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string Reason(Exception ex) => ex switch
    {
        InvalidDataException => ex.Message,
        _ => "the file is damaged or not a supported archive.",
    };

    // Limits across everything extracted for one version, counted from the bytes actually written.
    // Also remembers the files written, so a failed archive's files can be removed again.
    private sealed class Budget
    {
        private readonly List<string> _written = [];
        private long _bytes;

        public int Checkpoint() => _written.Count;

        public void RollBackTo(int checkpoint)
        {
            foreach (var path in _written[checkpoint..])
            {
                File.Delete(path);
            }

            _written.RemoveRange(checkpoint, _written.Count - checkpoint);
        }

        public void AddFile(string path)
        {
            if (_written.Count >= MaxFiles)
            {
                throw new InvalidDataException($"it contains more than {MaxFiles} files.");
            }

            _written.Add(path);
        }

        public void AddBytes(long count)
        {
            _bytes += count;
            if (_bytes > MaxTotalBytes)
            {
                throw new InvalidDataException($"its contents are larger than {MaxTotalBytes / 1024 / 1024} MB.");
            }
        }
    }
}
