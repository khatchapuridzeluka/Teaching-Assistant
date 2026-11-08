using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface IRemoteFileSource
{
    Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default);

    Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default);
}
