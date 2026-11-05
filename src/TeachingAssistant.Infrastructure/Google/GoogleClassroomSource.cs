using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Google;

/// <summary>
/// <see cref="ISubmissionSource"/> backed by the Google Classroom API, using the teacher's stored token.
/// Translates Google errors into submission-source exceptions.
/// </summary>
public class GoogleClassroomSource : ISubmissionSource
{
    private const string ServiceName = "Google Classroom";

    // Items per page; Google may return fewer, so paging always follows the next-page token.
    private const int PageSize = 100;

    private readonly GoogleAuthorization _authorization;

    public GoogleClassroomSource(GoogleAuthorization authorization)
    {
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        var request = classroom.Courses.List();
        request.TeacherId = "me"; // only courses the teacher teaches, not ones they're enrolled in
        request.PageSize = PageSize;

        var courses = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListCoursesResponse page) => (page.Courses, page.NextPageToken), cancellationToken);

        return courses.Select(c => new CourseResponse(c.Id, c.Name, c.Section, c.CourseState, c.AlternateLink)).ToList();
    }

    public async Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        // Without a state filter Classroom returns published work only, which is what students can submit to.
        var request = classroom.Courses.CourseWork.List(courseId);
        request.PageSize = PageSize;

        var courseWork = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListCourseWorkResponse page) => (page.CourseWork, page.NextPageToken), cancellationToken);

        return courseWork.Select(w => new AssignmentResponse(
            w.Id, w.CourseId, w.Title, w.Description, w.WorkType, w.MaxPoints,
            DueAt(w.DueDate, w.DueTime), w.CreationTimeDateTimeOffset, w.AlternateLink)).ToList();
    }

    public async Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        using var classroom = await CreateClassroomAsync(cancellationToken);

        // Submissions only carry a user ID; names come from the course roster.
        var names = await GetStudentNamesAsync(classroom, courseId, cancellationToken);

        var request = classroom.Courses.CourseWork.StudentSubmissions.List(courseId, assignmentId);
        request.PageSize = PageSize;

        var submissions = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListStudentSubmissionsResponse page) => (page.StudentSubmissions, page.NextPageToken), cancellationToken);

        return submissions.Select(s => new SubmissionResponse(
            s.Id, s.UserId, names.GetValueOrDefault(s.UserId, "Unknown student"), s.State, s.Late ?? false,
            LastTurnedInAt(s), s.UpdateTimeDateTimeOffset, Attachments(s), s.AlternateLink)).ToList();
    }

    private static async Task<Dictionary<string, string>> GetStudentNamesAsync(
        ClassroomService classroom, string courseId, CancellationToken cancellationToken)
    {
        var request = classroom.Courses.Students.List(courseId);
        request.PageSize = PageSize;

        var students = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListStudentsResponse page) => (page.Students, page.NextPageToken), cancellationToken);

        return students.ToDictionary(s => s.UserId, s => s.Profile?.Name?.FullName ?? s.UserId);
    }

    /// <summary>
    /// Runs a Classroom list request page by page until there is no next page, and returns all items.
    /// Classroom never returns a whole list at once: each page ends with a token that asks for the next one.
    /// </summary>
    /// <param name="request">The first request, with filters and page size already set.</param>
    /// <param name="setPageToken">Puts the next page's token on the request (each request type has its own PageToken property).</param>
    /// <param name="readPage">Takes the items and the next page's token out of one response.</param>
    private static Task<IList<TItem>> FetchAllAsync<TRequest, TResponse, TItem>(
        TRequest request,
        Action<TRequest, string?> setPageToken,
        Func<TResponse, (IList<TItem>? Items, string? NextPageToken)> readPage,
        CancellationToken cancellationToken)
        where TRequest : IClientServiceRequest<TResponse>
    {
        var streamer = new PageStreamer<TItem, TRequest, TResponse, string?>(
            setPageToken,
            page => readPage(page).NextPageToken is { Length: > 0 } token ? token : null, // empty token = last page
            page => readPage(page).Items ?? []);

        return GoogleErrors.TranslateAsync(ServiceName, () => streamer.FetchAllAsync(request, cancellationToken));
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

    private async Task<ClassroomService> CreateClassroomAsync(CancellationToken cancellationToken) =>
        new(new BaseClientService.Initializer
        {
            HttpClientInitializer = await _authorization.GetCredentialAsync(cancellationToken),
            ApplicationName = GoogleAuthorization.ApplicationName,
        });
}
