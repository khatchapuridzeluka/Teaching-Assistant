using System.Collections.Frozen;
using System.Runtime.ExceptionServices;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// <see cref="IRemoteFileSource"/> backed by the Google Drive API, using the teacher's stored token (drive.readonly).
/// Submission attachments in Classroom are Drive files; this reads their metadata and content.
/// </summary>
public class GoogleDriveFileSource : IRemoteFileSource
{
    private const string ServiceName = "Google Drive";
    private const string GoogleAppsPrefix = "application/vnd.google-apps.";
    private const string PdfMimeType = "application/pdf";

    // Only what we use; Drive returns a minimal set of fields unless asked.
    private const string MetadataFields = "id,name,mimeType,md5Checksum,size,modifiedTime,version";

    // Documents made in Google's editors have no file bytes; Drive exports them. PDF is readable by the model as is.
    private static readonly FrozenSet<string> ExportableAsPdf = new[]
    {
        "application/vnd.google-apps.document",
        "application/vnd.google-apps.spreadsheet",
        "application/vnd.google-apps.presentation",
        "application/vnd.google-apps.drawing",
    }.ToFrozenSet();

    private readonly GoogleAuthorization _authorization;

    public GoogleDriveFileSource(GoogleAuthorization authorization)
    {
        _authorization = authorization;
    }

    public async Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        using var drive = await CreateDriveAsync(cancellationToken);

        var request = drive.Files.Get(fileId);
        request.Fields = MetadataFields;
        request.SupportsAllDrives = true;

        var file = await GoogleErrors.TranslateAsync(ServiceName, () => request.ExecuteAsync(cancellationToken));
        return ToRemoteFile(file);
    }

    public async Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default)
    {
        if (!file.IsDownloadable)
            throw new InvalidOperationException($"'{file.Name}' ({file.MimeType}) has no content to download.");

        using var drive = await CreateDriveAsync(cancellationToken);

        await GoogleErrors.TranslateAsync(ServiceName, async () =>
        {
            var progress = file.MimeType.StartsWith(GoogleAppsPrefix, StringComparison.Ordinal)
                ? await drive.Files.Export(file.Id, PdfMimeType).DownloadAsync(destination, cancellationToken)
                : await DownloadUploadedFileAsync(drive, file.Id, destination, cancellationToken);

            // Downloads report failure in the result instead of throwing; rethrow so it is translated like any Google error.
            if (progress.Status == DownloadStatus.Failed)
                ExceptionDispatchInfo.Throw(progress.Exception
                    ?? new HttpRequestException($"Download of '{file.Name}' failed after {progress.BytesDownloaded} bytes."));

            return progress;
        });
    }

    private static Task<IDownloadProgress> DownloadUploadedFileAsync(
        DriveService drive, string fileId, Stream destination, CancellationToken cancellationToken)
    {
        var request = drive.Files.Get(fileId);
        request.SupportsAllDrives = true;
        return request.DownloadAsync(destination, cancellationToken);
    }

    private static RemoteFile ToRemoteFile(DriveFile file)
    {
        var exportedAsPdf = ExportableAsPdf.Contains(file.MimeType);
        var isGoogleApp = file.MimeType.StartsWith(GoogleAppsPrefix, StringComparison.Ordinal);

        return new RemoteFile(
            file.Id,
            file.Name,
            file.MimeType,
            file.Size,
            // Uploaded files have an md5 checksum; Google Docs don't, but their version number increases with every edit.
            file.Md5Checksum ?? $"version:{file.Version}",
            file.ModifiedTimeDateTimeOffset,
            exportedAsPdf && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? file.Name + ".pdf" : file.Name,
            exportedAsPdf ? PdfMimeType : file.MimeType,
            IsDownloadable: !isGoogleApp || exportedAsPdf);
    }

    private async Task<DriveService> CreateDriveAsync(CancellationToken cancellationToken) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = await _authorization.GetCredentialAsync(cancellationToken),
            ApplicationName = GoogleAuthorization.ApplicationName,
        });
}
