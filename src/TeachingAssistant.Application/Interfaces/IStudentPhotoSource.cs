namespace TeachingAssistant.Application.Interfaces;

public record DownloadedPhoto
{
    public required byte[] Bytes { get; init; }
    public required string ContentType { get; init; }
}

public interface IStudentPhotoSource
{
    Task<DownloadedPhoto> DownloadAsync(string photoUrl, CancellationToken cancellationToken = default);
}

public interface IStudentPhotoStorage
{
    Task<string> SaveAsync(string externalUserId, DownloadedPhoto photo, CancellationToken cancellationToken = default);

    Stream? Open(string storedName);

    void Delete(string storedName);
}
