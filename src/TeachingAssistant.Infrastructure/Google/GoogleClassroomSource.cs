using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Classroom.v1;
using Google.Apis.Requests;
using Google.Apis.Services;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// <see cref="ISubmissionSource"/> backed by the Google Classroom API, using the teacher's stored token.
/// Translates Google errors into submission-source exceptions.
/// </summary>
public class GoogleClassroomSource : ISubmissionSource
{
    private const string ApplicationName = "Teaching Assistant";

    private readonly GoogleAuthorization _authorization;

    public GoogleClassroomSource(GoogleAuthorization authorization)
    {
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        var courses = new List<CourseResponse>();
        string? pageToken = null;
        do
        {
            var request = classroom.Courses.List();
            request.TeacherId = "me"; // only courses the teacher teaches, not ones they're enrolled in
            request.PageSize = 100;
            request.PageToken = pageToken;

            var page = await ExecuteAsync(request, cancellationToken);
            courses.AddRange((page.Courses ?? []).Select(c =>
                new CourseResponse(c.Id, c.Name, c.Section, c.CourseState, c.AlternateLink)));
            pageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return courses;
    }

    private async Task<ClassroomService> CreateClassroomAsync(CancellationToken cancellationToken)
    {
        if (!_authorization.TryGetFlow(out var flow))
            throw new SubmissionSourceNotConnectedException(
                "Google:ClientId and Google:ClientSecret are not set. Add them with dotnet user-secrets.");

        var token = await flow.LoadTokenAsync(GoogleAuthorization.UserKey, cancellationToken)
            ?? throw new SubmissionSourceNotConnectedException(
                "Google account is not connected. Call POST /auth/google/connect.");

        return new ClassroomService(new BaseClientService.Initializer
        {
            HttpClientInitializer = new UserCredential(flow, GoogleAuthorization.UserKey, token),
            ApplicationName = ApplicationName,
        });
    }

    private static async Task<T> ExecuteAsync<T>(ClientServiceRequest<T> request, CancellationToken cancellationToken)
    {
        try
        {
            return await request.ExecuteAsync(cancellationToken);
        }
        catch (TokenResponseException ex)
        {
            throw new SubmissionSourceNotConnectedException(
                $"Google authorization is no longer valid ({ex.Error.Error}). Call POST /auth/google/connect.", ex);
        }
        catch (GoogleApiException ex)
        {
            throw new SubmissionSourceUnavailableException(
                $"Google Classroom returned {(int)ex.HttpStatusCode}: {ex.Error?.Message ?? ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SubmissionSourceUnavailableException("Could not reach Google Classroom (network error).", ex);
        }
    }
}
