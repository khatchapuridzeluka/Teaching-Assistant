using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.Storage;

/// <summary>
/// <see cref="ISnapshotStorage"/> on the local disk. Layout, readable when browsing:
/// {Root}/submissions/{course}/{assignment}/{student} ({id})/v{n}/original/ (as downloaded) and code/ (extracted).
/// Each version is written into {Root}/tmp/ first and moved into place on commit, so a crash or failed download
/// never leaves a half-written version folder.
/// </summary>
public class FileSystemSnapshotStorage(IOptions<StorageOptions> options) : ISnapshotStorage
{
    // Characters of the student ID kept in the folder name; enough to tell same-named students apart.
    private const int StudentIdSuffixLength = 6;

    private const string OriginalFolder = "original";
    private const string CodeFolder = "code";

    private readonly string _root = options.Value.Root;

    public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId)
    {
        var idSuffix = studentId.Length > StudentIdSuffixLength ? studentId[^StudentIdSuffixLength..] : studentId;
        return $"submissions/{SafeName(courseName)}/{SafeName(assignmentTitle)}/{SafeName(studentName)} ({SafeName(idSuffix)})";
    }

    public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber)
    {
        var relativePath = $"{submissionFolder}/v{versionNumber}";
        var tempPath = Path.Combine(_root, "tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempPath, "original"));
        return new Writer(relativePath, tempPath, Path.Combine(_root, relativePath));
    }

    public Stream? OpenOriginal(string versionPath, string storedName)
    {
        var root = Path.GetFullPath(_root);
        var path = Path.GetFullPath(Path.Combine(root, versionPath, "original", storedName));

        // The arguments come from our database, but check anyway: never serve anything outside the storage root.
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    public Task<CodeExtraction> ReExtractAsync(
        string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default)
    {
        var versionDirectory = Path.Combine(_root, versionPath);
        var codeDirectory = Path.Combine(versionDirectory, CodeFolder);
        var staging = Path.Combine(versionDirectory, $"code.new-{Guid.NewGuid():N}");

        try
        {
            // Build the new code/ beside the old one and swap at the end, so a failure leaves the old code in place.
            var result = CodeExtractor.Extract(Path.Combine(versionDirectory, OriginalFolder), staging, files, cancellationToken);
            if (Directory.Exists(codeDirectory))
                Directory.Delete(codeDirectory, recursive: true);
            Directory.Move(staging, codeDirectory);
            return Task.FromResult(result);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
        CodeExtractor.List(Path.Combine(_root, versionPath, CodeFolder));

    public Stream? OpenCode(string versionPath, string relativePath)
    {
        var codeDirectory = Path.GetFullPath(Path.Combine(_root, versionPath, CodeFolder));
        var path = Path.GetFullPath(Path.Combine(codeDirectory, relativePath));

        // relativePath comes from the browser: only ever serve files inside this version's code/ folder.
        if (!path.StartsWith(codeDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    // Keeps names readable (including Georgian letters) but removes anything that is not valid in a file or folder
    // name or could point outside the folder ("..", slashes).
    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        // Slashes are replaced (not cut off), so "OOP / Patterns" stays one readable folder name.
        var cleaned = new string(name
                .Select(c => invalid.Contains(c) || c is '/' or '\\' || char.IsControl(c) ? '_' : c).ToArray())
            .Trim().Trim('.');

        if (cleaned.Length > 120)
            cleaned = cleaned[..(120 - Path.GetExtension(cleaned).Length)] + Path.GetExtension(cleaned);

        return cleaned.Length == 0 ? "unnamed" : cleaned;
    }

    private sealed class Writer(string relativePath, string tempPath, string finalPath) : ISnapshotWriter
    {
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
        private bool _committed;

        public string RelativePath => relativePath;

        public async Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default)
        {
            var storedName = UniqueName(SafeName(fileName));
            await using var stream = new FileStream(Path.Combine(tempPath, "original", storedName), FileMode.CreateNew);
            await write(stream);
            return storedName;
        }

        public Task<CodeExtraction> ExtractCodeAsync(IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            Task.FromResult(CodeExtractor.Extract(
                Path.Combine(tempPath, OriginalFolder), Path.Combine(tempPath, CodeFolder), files, cancellationToken));

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            // A folder already at the final path is left over from a fetch whose database save failed
            // (no version row points to it), so it is safe to replace.
            if (Directory.Exists(finalPath))
                Directory.Delete(finalPath, recursive: true);

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            Directory.Move(tempPath, finalPath);
            _committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_committed && Directory.Exists(tempPath))
                Directory.Delete(tempPath, recursive: true);
            return ValueTask.CompletedTask;
        }

        // Two attachments with the same name both get stored: "report.pdf", then "report (2).pdf".
        // Locked because a version's files are downloaded in parallel.
        private string UniqueName(string name)
        {
            lock (_names)
            {
                var candidate = name;
                for (var i = 2; !_names.Add(candidate); i++)
                    candidate = $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}";
                return candidate;
            }
        }
    }
}
