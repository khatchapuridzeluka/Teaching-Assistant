using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface ISnapshotWriter : IAsyncDisposable
{
    string RelativePath { get; }

    Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default);

    Task<CodeExtraction> ExtractCodeAsync(IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);
}
