using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using TeachingAssistant.Application.Exceptions;

namespace TeachingAssistant.Infrastructure.Google;

internal static class GoogleErrors
{
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
