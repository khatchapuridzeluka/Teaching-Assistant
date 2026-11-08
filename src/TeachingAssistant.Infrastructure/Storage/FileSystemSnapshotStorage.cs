using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.Storage;

public class FileSystemSnapshotStorage(IOptions<StorageOptions> options) : ISnapshotStorage
{
    private const int StudentIdSuffixLength = 6;

    private const string OriginalFolder = "original";
    private const string CodeFolder = "code";

    private readonly string _root = options.Value.Root;

    public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId)
    {
        var idSuffix = studentId.Length > StudentIdSuffixLength ? studentId[^StudentIdSuffixLength..] : studentId;
        return $"submissions/{SafeName(courseName)}/{SafeName(assignmentTitle)}/{SafeName(studentName)} ({SafeName(idSuffix)})";
    }

    public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId)
    {
        var idSuffix = assignmentId.Length > StudentIdSuffixLength ? assignmentId[^StudentIdSuffixLength..] : assignmentId;
        return $"instructions/{SafeName(courseName)}/{SafeName(assignmentTitle)} ({SafeName(idSuffix)})";
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
        var path = OriginalPath(versionPath, storedName);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    public bool OriginalExists(string versionPath, string storedName) =>
        OriginalPath(versionPath, storedName) is { } path && File.Exists(path);

    public async Task RestoreOriginalAsync(
        string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default)
    {
        var path = OriginalPath(versionPath, storedName)
                   ?? throw new ArgumentException($"'{storedName}' is outside the storage root.", nameof(storedName));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = $"{path}.{Guid.NewGuid():N}.part";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew))
            {
                await write(stream);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private string? OriginalPath(string versionPath, string storedName)
    {
        var root = Path.GetFullPath(_root);
        var path = Path.GetFullPath(Path.Combine(root, versionPath, OriginalFolder, storedName));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    public Task<CodeExtraction> ReExtractAsync(
        string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default)
    {
        var versionDirectory = Path.Combine(_root, versionPath);
        var codeDirectory = Path.Combine(versionDirectory, CodeFolder);
        var staging = Path.Combine(versionDirectory, $"code.new-{Guid.NewGuid():N}");

        try
        {
            var result = CodeExtractor.Extract(Path.Combine(versionDirectory, OriginalFolder), staging, files, cancellationToken);
            if (Directory.Exists(codeDirectory))
            {
                Directory.Delete(codeDirectory, recursive: true);
            }

            Directory.Move(staging, codeDirectory);
            return Task.FromResult(result);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
        CodeExtractor.List(Path.Combine(_root, versionPath, CodeFolder));

    public Stream? OpenCode(string versionPath, string relativePath)
    {
        var codeDirectory = Path.GetFullPath(Path.Combine(_root, versionPath, CodeFolder));
        var path = Path.GetFullPath(Path.Combine(codeDirectory, relativePath));

        if (!path.StartsWith(codeDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name
                .Select(c => invalid.Contains(c) || c is '/' or '\\' || char.IsControl(c) ? '_' : c).ToArray())
            .Trim().Trim('.');

        if (cleaned.Length > 120)
        {
            cleaned = cleaned[..(120 - Path.GetExtension(cleaned).Length)] + Path.GetExtension(cleaned);
        }

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
            if (Directory.Exists(finalPath))
            {
                Directory.Delete(finalPath, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            Directory.Move(tempPath, finalPath);
            _committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_committed && Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }

        private string UniqueName(string name)
        {
            lock (_names)
            {
                var candidate = name;
                for (var i = 2; !_names.Add(candidate); i++)
                {
                    candidate = $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}";
                }

                return candidate;
            }
        }
    }
}
