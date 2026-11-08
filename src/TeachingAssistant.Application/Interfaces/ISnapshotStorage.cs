using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface ISnapshotStorage
{
    string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId);

    string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId);

    ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber);

    Stream? OpenOriginal(string versionPath, string storedName);

    bool OriginalExists(string versionPath, string storedName);

    Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default);

    Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default);

    IReadOnlyList<CodeFile> ListCode(string versionPath);

    Stream? OpenCode(string versionPath, string relativePath);
}

public interface ISnapshotWriter : IAsyncDisposable
{
    string RelativePath { get; }

    Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default);

    Task<CodeExtraction> ExtractCodeAsync(IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);
}
