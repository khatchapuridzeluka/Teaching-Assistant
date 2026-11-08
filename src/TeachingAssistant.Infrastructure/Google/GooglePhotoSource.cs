using Microsoft.Extensions.Logging;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// <see cref="IStudentPhotoSource"/> over plain HTTP: Classroom's photo URLs point at Google's public image servers, so no
/// token is needed. Only images up to a small size are accepted. A failed photo is logged with its status, since the sync
/// itself only counts failures.
/// </summary>
public class GooglePhotoSource(HttpClient http, ILogger<GooglePhotoSource> logger) : IStudentPhotoSource
{
    // A 128-pixel profile photo is a few KB; anything far larger is not what we asked for.
    private const long MaxBytes = 2 * 1024 * 1024;

    public async Task<DownloadedPhoto> DownloadAsync(string photoUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(photoUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw Failed(photoUrl, $"The photo returned {(int)response.StatusCode}.");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw Failed(photoUrl, $"The photo URL returned {contentType}, not an image.");
            }

            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                throw Failed(photoUrl, "The photo is too large.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > MaxBytes)
            {
                throw Failed(photoUrl, "The photo is too large.");
            }

            return new DownloadedPhoto(bytes, contentType);
        }
        catch (HttpRequestException ex)
        {
            throw Failed(photoUrl, $"The photo could not be downloaded: {ex.Message}");
        }
    }

    private SubmissionSourceUnavailableException Failed(string photoUrl, string reason)
    {
        logger.LogWarning("Student photo not stored: {Reason} ({PhotoUrl})", reason, photoUrl);
        return new SubmissionSourceUnavailableException(reason);
    }
}
