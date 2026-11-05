using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// Turns Google's exceptions into submission-source exceptions, which the API maps to 409/404/502.
/// Shared by every Google adapter so errors look the same whichever Google API failed.
/// </summary>
internal static class GoogleErrors
{
    /// <param name="serviceName">Shown in messages, e.g. "Google Classroom" or "Google Drive".</param>
    /// <param name="call">The Google call to run.</param>
    public static async Task<T> TranslateAsync<T>(string serviceName, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (TokenResponseException ex)
        {
            throw new SubmissionSourceNotConnectedException(
                $"Google authorization is no longer valid ({ex.Error.Error}). Call POST /auth/google/connect.", ex);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            throw new SubmissionSourceNotFoundException(
                $"Not found in {serviceName}: {ex.Error?.Message ?? ex.Message}", ex);
        }
        catch (GoogleApiException ex)
        {
            throw new SubmissionSourceUnavailableException(
                $"{serviceName} returned {(int)ex.HttpStatusCode}: {ex.Error?.Message ?? ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SubmissionSourceUnavailableException($"Could not reach {serviceName} (network error).", ex);
        }
    }
}
