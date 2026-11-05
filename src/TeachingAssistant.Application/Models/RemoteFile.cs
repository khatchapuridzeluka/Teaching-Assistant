namespace TeachingAssistant.Application.Models;

/// <summary>
/// A file at its source (Google Drive), described without downloading it.
/// </summary>
/// <param name="Id">The source's file ID.</param>
/// <param name="Name">The file name as the student uploaded it.</param>
/// <param name="MimeType">The source's type, e.g. application/zip or application/vnd.google-apps.document.</param>
/// <param name="SizeBytes">Null for documents created in Google Docs/Sheets/Slides, which have no file of their own.</param>
/// <param name="Revision">Changes whenever the content changes: the md5 checksum for uploaded files, a version number for Google Docs.</param>
/// <param name="ModifiedAt">When the content last changed.</param>
/// <param name="DownloadName">The name the downloaded bytes should get (Google Docs are exported as .pdf).</param>
/// <param name="DownloadMimeType">The type of the downloaded bytes.</param>
/// <param name="IsDownloadable">False for things without content to download, such as folders, shortcuts or forms.</param>
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
