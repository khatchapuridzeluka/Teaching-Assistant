namespace TeachingAssistant.Application.Models;

public record RemoteFile(
    string Id,
    string Name,
    string MimeType,
    long? SizeBytes,
    string Revision,
    DateTimeOffset? ModifiedAt,
    string DownloadName,
    string DownloadMimeType,
    bool IsDownloadable);
