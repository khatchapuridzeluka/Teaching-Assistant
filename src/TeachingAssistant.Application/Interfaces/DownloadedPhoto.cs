namespace TeachingAssistant.Application.Interfaces;

public record DownloadedPhoto
{
    public required byte[] Bytes { get; init; }
    public required string ContentType { get; init; }
}
