using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
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

    public async Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        var assignments = new List<AssignmentResponse>();
        string? pageToken = null;
        do
        {
            // Without a state filter Classroom returns published work only, which is what students can submit to.
            var request = classroom.Courses.CourseWork.List(courseId);
            request.PageSize = 100;
            request.PageToken = pageToken;

            var page = await ExecuteAsync(request, cancellationToken);
            assignments.AddRange((page.CourseWork ?? []).Select(w => new AssignmentResponse(
                w.Id, w.CourseId, w.Title, w.Description, w.WorkType, w.MaxPoints,
                DueAt(w.DueDate, w.DueTime), w.CreationTimeDateTimeOffset, w.AlternateLink)));
            pageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return assignments;
    }

    public async Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        // Submissions only carry a user ID; names come from the course roster.
        var names = await GetStudentNamesAsync(classroom, courseId, cancellationToken);

        var submissions = new List<SubmissionResponse>();
        string? pageToken = null;
        do
        {
            var request = classroom.Courses.CourseWork.StudentSubmissions.List(courseId, assignmentId);
            request.PageSize = 100;
            request.PageToken = pageToken;

            var page = await ExecuteAsync(request, cancellationToken);
            submissions.AddRange((page.StudentSubmissions ?? []).Select(s => new SubmissionResponse(
                s.Id, s.UserId, names.GetValueOrDefault(s.UserId, "Unknown student"), s.State, s.Late ?? false,
                LastTurnedInAt(s), s.UpdateTimeDateTimeOffset, Attachments(s), s.AlternateLink)));
            pageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return submissions;
    }

    private static async Task<Dictionary<string, string>> GetStudentNamesAsync(
        ClassroomService classroom, string courseId, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>();
        string? pageToken = null;
        do
        {
            var request = classroom.Courses.Students.List(courseId);
            request.PageSize = 100;
            request.PageToken = pageToken;

            var page = await ExecuteAsync(request, cancellationToken);
            foreach (var student in page.Students ?? [])
                names[student.UserId] = student.Profile?.Name?.FullName ?? student.UserId;
            pageToken = page.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return names;
    }

    // The latest turn-in from the history; every resubmission adds a new TURNED_IN entry.
    private static DateTimeOffset? LastTurnedInAt(StudentSubmission submission) =>
        (submission.SubmissionHistory ?? [])
            .Select(h => h.StateHistory)
            .Where(h => h?.State == "TURNED_IN")
            .Max(h => h!.StateTimestampDateTimeOffset);

    private static List<SubmissionAttachment> Attachments(StudentSubmission submission) =>
        (submission.AssignmentSubmission?.Attachments ?? []).Select(a => a switch
        {
            { DriveFile: { } file } => new SubmissionAttachment(AttachmentKind.DriveFile, file.Title, file.AlternateLink, file.Id),
            { Link: { } link } => new SubmissionAttachment(AttachmentKind.Link, link.Title ?? link.Url, link.Url, null),
            _ => new SubmissionAttachment(AttachmentKind.Other,
                a.YouTubeVideo?.Title ?? a.Form?.Title ?? "Unsupported attachment", null, null),
        }).ToList();

    // Classroom splits the deadline into a date and a time of day, both in UTC.
    private static DateTimeOffset? DueAt(Date? date, TimeOfDay? time) =>
        date is { Year: { } year, Month: { } month, Day: { } day }
            ? new DateTimeOffset(year, month, day, time?.Hours ?? 0, time?.Minutes ?? 0, 0, TimeSpan.Zero)
            : null;

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
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            throw new SubmissionSourceNotFoundException(
                $"Not found in Google Classroom: {ex.Error?.Message ?? ex.Message}", ex);
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
