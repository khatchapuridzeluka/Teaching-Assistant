namespace TeachingAssistant.Application.Models;

public record RemoteFile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public required string Revision { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public required string DownloadName { get; init; }
    public required string DownloadMimeType { get; init; }
    public required bool IsDownloadable { get; init; }
}
