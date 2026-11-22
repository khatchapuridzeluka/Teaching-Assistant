using System.Collections.Frozen;
using System.Runtime.ExceptionServices;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace TeachingAssistant.Infrastructure.Google;

public class GoogleDriveFileSource : IRemoteFileSource
{
    private const string ServiceName = "Google Drive";
    private const string GoogleAppsPrefix = "application/vnd.google-apps.";
    private const string PdfMimeType = "application/pdf";

    private const string MetadataFields = "id,name,mimeType,md5Checksum,size,modifiedTime,version";

    private static readonly FrozenSet<string> ExportableAsPdf = new[]
    {
        "application/vnd.google-apps.document",
        "application/vnd.google-apps.spreadsheet",
        "application/vnd.google-apps.presentation",
        "application/vnd.google-apps.drawing",
    }.ToFrozenSet();

    private readonly GoogleAuthorization _authorization;
    private volatile Client? _client;

    public GoogleDriveFileSource(GoogleAuthorization authorization)
    {
        _authorization = authorization;
    }

    public async Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var drive = await GetDriveAsync(cancellationToken);

        var request = drive.Files.Get(fileId);
        request.Fields = MetadataFields;
        request.SupportsAllDrives = true;

        var file = await GoogleErrors.TranslateAsync(ServiceName, () => request.ExecuteAsync(cancellationToken));
        return ToRemoteFile(file);
    }

    public async Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default)
    {
        if (!file.IsDownloadable)
        {
            throw new InvalidOperationException($"'{file.Name}' ({file.MimeType}) has no content to download.");
        }

        var drive = await GetDriveAsync(cancellationToken);

        await GoogleErrors.TranslateAsync(ServiceName, async () =>
        {
            var progress = file.MimeType.StartsWith(GoogleAppsPrefix, StringComparison.Ordinal)
                ? await drive.Files.Export(file.Id, PdfMimeType).DownloadAsync(destination, cancellationToken)
                : await DownloadUploadedFileAsync(drive, file.Id, destination, cancellationToken);

            if (progress.Status == DownloadStatus.Failed)
            {
                ExceptionDispatchInfo.Throw(progress.Exception
                    ?? new HttpRequestException($"Download of '{file.Name}' failed after {progress.BytesDownloaded} bytes."));
            }

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

        return new RemoteFile
        {
            Id = file.Id,
            Name = file.Name,
            MimeType = file.MimeType,
            SizeBytes = file.Size,
            Revision = file.Md5Checksum ?? $"version:{file.Version}",
            ModifiedAt = file.ModifiedTimeDateTimeOffset,
            DownloadName = exportedAsPdf && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? file.Name + ".pdf" : file.Name,
            DownloadMimeType = exportedAsPdf ? PdfMimeType : file.MimeType,
            IsDownloadable = !isGoogleApp || exportedAsPdf,
        };
    }

    private async Task<DriveService> GetDriveAsync(CancellationToken cancellationToken)
    {
        var credential = await _authorization.GetCredentialAsync(cancellationToken);
        if (_client is { } client && ReferenceEquals(client.Credential, credential))
        {
            return client.Service;
        }

        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = GoogleAuthorization.ApplicationName,
        });
        _client = new Client { Credential = credential, Service = service };
        return service;
    }

    private sealed record Client
    {
        public required UserCredential Credential { get; init; }
        public required DriveService Service { get; init; }
    }
}
