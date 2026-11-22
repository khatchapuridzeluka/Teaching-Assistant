namespace TeachingAssistant.Application.Interfaces;

public interface IStudentPhotoStorage
{
    Task<string> SaveAsync(string externalUserId, DownloadedPhoto photo, CancellationToken cancellationToken = default);

    Stream? Open(string storedName);

    void Delete(string storedName);
}
