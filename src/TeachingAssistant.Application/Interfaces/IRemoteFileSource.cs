using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Files attached to submissions, read from where they are stored (Google Drive). Read-only.
/// Throws <see cref="Exceptions.SubmissionSourceNotConnectedException"/> when the account is not connected,
/// <see cref="Exceptions.SubmissionSourceNotFoundException"/> when the file does not exist or is not visible,
/// and <see cref="Exceptions.SubmissionSourceUnavailableException"/> when the source fails.
/// </summary>
public interface IRemoteFileSource
{
    /// <summary>
    /// Reads a file's metadata, including its revision, so callers can tell whether it changed without downloading it.
    /// </summary>
    Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the file's content to the destination: uploaded files as they are, Google Docs/Sheets/Slides exported as PDF.
    /// Only call for files with <see cref="RemoteFile.IsDownloadable"/> set. On failure the destination may hold partial content.
    /// </summary>
    Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default);
}
