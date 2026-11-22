using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Tests.Application;

/// <summary>Extracted code by path; everything else a snapshot storage does is not used here.</summary>
internal sealed class FakeCodeStorage : ISnapshotStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
        Files.OrderBy(f => f.Key).Select(f => new CodeFile { Path = f.Key, SizeBytes = f.Value.Length }).ToList();

    public Stream? OpenCode(string versionPath, string relativePath) =>
        Files.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes) : null;

    public bool OriginalExists(string versionPath, string storedName) => throw new NotSupportedException();

    public Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Stream? OpenOriginal(string versionPath, string storedName) => throw new NotSupportedException();

    public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
        throw new NotSupportedException();

    public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) =>
        throw new NotSupportedException();

    public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();

    public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
