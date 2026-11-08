namespace TeachingAssistant.Application.Interfaces;

/// <summary>A downloaded profile photo.</summary>
public record DownloadedPhoto(byte[] Bytes, string ContentType);

/// <summary>
/// Downloads a student's profile photo from the URL the source reported (Google's image servers). Throws
/// <see cref="Exceptions.SubmissionSourceUnavailableException"/> when it cannot be downloaded or is not an image.
/// </summary>
public interface IStudentPhotoSource
{
    Task<DownloadedPhoto> DownloadAsync(string photoUrl, CancellationToken cancellationToken = default);
}

/// <summary>Our copies of students' profile photos: one file per student, replaced in place (no history).</summary>
public interface IStudentPhotoStorage
{
    /// <summary>Writes (or replaces) the student's photo and returns the name it is stored under.</summary>
    Task<string> SaveAsync(string externalUserId, DownloadedPhoto photo, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored photo; null when the file is gone.</summary>
    Stream? Open(string storedName);

    void Delete(string storedName);
}
