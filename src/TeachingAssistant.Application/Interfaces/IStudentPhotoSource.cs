namespace TeachingAssistant.Application.Interfaces;

public record DownloadedPhoto(byte[] Bytes, string ContentType);

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
