namespace TeachingAssistant.Application.Interfaces;

public interface IStudentPhotoSource
{
    Task<DownloadedPhoto> DownloadAsync(string photoUrl, CancellationToken cancellationToken = default);
}
