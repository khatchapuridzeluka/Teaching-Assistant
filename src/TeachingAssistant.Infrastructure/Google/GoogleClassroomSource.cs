using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Requests;
using Google.Apis.Services;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;

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

    // Classroom's course work ID for "all assignments of the course" when listing submissions.
    private const string AllCourseWork = "-";

    private readonly GoogleAuthorization _authorization;
    private volatile Client? _client;

    public GoogleClassroomSource(GoogleAuthorization authorization)
    {
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

        var request = classroom.Courses.List();
        request.TeacherId = "me"; // only courses the teacher teaches, not ones they're enrolled in
        request.PageSize = PageSize;

        var courses = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListCoursesResponse page) => (page.Courses, page.NextPageToken), cancellationToken);

        return courses.Select(ToCourse).ToList();
    }

    public async Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);
        var course = await GoogleErrors.TranslateAsync(ServiceName,
            () => classroom.Courses.Get(courseId).ExecuteAsync(cancellationToken));
        return ToCourse(course);
    }

    public async Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

        // Without a state filter Classroom returns published work only, which is what students can submit to.
        var request = classroom.Courses.CourseWork.List(courseId);
        request.PageSize = PageSize;

        var courseWork = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListCourseWorkResponse page) => (page.CourseWork, page.NextPageToken), cancellationToken);

        return courseWork.Select(ToAssignment).ToList();
    }

    public async Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);
        var courseWork = await GoogleErrors.TranslateAsync(ServiceName,
            () => classroom.Courses.CourseWork.Get(courseId, assignmentId).ExecuteAsync(cancellationToken));
        return ToAssignment(courseWork);
    }

    private static CourseResponse ToCourse(Course c) => new(c.Id, c.Name, c.Section, c.CourseState, c.AlternateLink);

    private static AssignmentResponse ToAssignment(CourseWork w) => new(
        w.Id, w.CourseId, w.Title, w.Description, w.WorkType, w.MaxPoints,
        DueAt(w.DueDate, w.DueTime), w.CreationTimeDateTimeOffset, w.AlternateLink, Materials(w));

    // The teacher's attachments on the assignment (instructions, grading scheme). Drive files are wrapped in a
    // SharedDriveFile here, unlike student attachments.
    private static List<SubmissionAttachment> Materials(CourseWork courseWork) =>
        (courseWork.Materials ?? []).Select(m => m switch
        {
            { DriveFile.DriveFile: { } file } => new SubmissionAttachment(AttachmentKind.DriveFile, file.Title, file.AlternateLink, file.Id),
            { Link: { } link } => new SubmissionAttachment(AttachmentKind.Link, link.Title ?? link.Url, link.Url, null),
            _ => new SubmissionAttachment(AttachmentKind.Other,
                m.YoutubeVideo?.Title ?? m.Form?.Title ?? "Unsupported material", null, null),
        }).ToList();

    public async Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

        // Submissions only carry a user ID; names come from the course roster. The two lists do not depend on each
        // other, so both requests run at the same time.
        var namesCall = GetStudentNamesAsync(classroom, courseId, cancellationToken);

        var submissionsCall = ListSubmissionsAsync(classroom, courseId, assignmentId, cancellationToken);

        await Task.WhenAll(namesCall, submissionsCall);
        var (names, submissions) = (await namesCall, await submissionsCall);

        return submissions.Select(s => ToSubmission(s, names)).ToList();
    }

    public async Task<IReadOnlyList<SubmissionResponse>> GetCourseSubmissionsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

        // "-" as the course work ID asks for every assignment of the course in one list: a class of 15 students with
        // 5 assignments is one page.
        var namesCall = GetStudentNamesAsync(classroom, courseId, cancellationToken);
        var submissionsCall = ListSubmissionsAsync(classroom, courseId, AllCourseWork, cancellationToken);

        await Task.WhenAll(namesCall, submissionsCall);
        var (names, submissions) = (await namesCall, await submissionsCall);

        return submissions.Select(s => ToSubmission(s, names)).ToList();
    }

    public async Task<IReadOnlyList<RosterStudent>> GetStudentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);
        var request = classroom.Courses.Students.List(courseId);
        request.PageSize = PageSize;

        var students = await FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListStudentsResponse page) => (page.Students, page.NextPageToken), cancellationToken);

        return students
            .Select(s => new RosterStudent(s.UserId, s.Profile?.Name?.FullName ?? s.UserId, PhotoUrl(s.Profile?.PhotoUrl)))
            .ToList();
    }

    // Classroom's photo URL in a fixed small size, or null for none and for Google's default picture. Photos need the
    // classroom.profile.photos scope; without it the URL is simply missing.
    private static string? PhotoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains("default-user", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var absolute = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;

        // Newer photo URLs (…/a/ACg8oc…) take a size option at the end (=s100, =s96-c): ask for 128 pixels. Older ones end
        // in a file name (…/photo.jpg) where an appended option breaks the link, so they are used as given.
        const string sizeOption = "=s\\d+(-[a-z]+)?$";
        if (System.Text.RegularExpressions.Regex.IsMatch(absolute, sizeOption))
        {
            return System.Text.RegularExpressions.Regex.Replace(absolute, sizeOption, "=s128-c");
        }

        var lastSegment = absolute[(absolute.LastIndexOf('/') + 1)..];
        return lastSegment.Contains('.') || lastSegment.Contains('=') ? absolute : absolute + "=s128-c";
    }

    private static Task<IList<StudentSubmission>> ListSubmissionsAsync(
        ClassroomService classroom, string courseId, string courseWorkId, CancellationToken cancellationToken)
    {
        var request = classroom.Courses.CourseWork.StudentSubmissions.List(courseId, courseWorkId);
        request.PageSize = PageSize;

        return FetchAllAsync(request, (r, token) => r.PageToken = token,
            (ListStudentSubmissionsResponse page) => (page.StudentSubmissions, page.NextPageToken), cancellationToken);
    }

    private static SubmissionResponse ToSubmission(StudentSubmission s, Dictionary<string, string> names) => new(
        s.Id, s.CourseWorkId, s.UserId, names.GetValueOrDefault(s.UserId, "Unknown student"), s.State, s.Late ?? false,
        LastTurnedInAt(s), s.UpdateTimeDateTimeOffset, Attachments(s), s.AlternateLink, s.AssignedGrade, GradeHistory(s));

    // Every grade change Classroom recorded, oldest first. Grades are only read here; this app never writes them.
    private static List<GradeChange> GradeHistory(StudentSubmission submission) =>
        (submission.SubmissionHistory ?? [])
            .Select(h => h.GradeHistory)
            .Where(g => g?.GradeTimestampDateTimeOffset is not null)
            .Select(g => new GradeChange(GradeKind(g!.GradeChangeType), g.PointsEarned, g.MaxPoints, g.GradeTimestampDateTimeOffset!.Value))
            .OrderBy(g => g.At)
            .ToList();

    private static GradeEventKind GradeKind(string? changeType) => changeType switch
    {
        "ASSIGNED_GRADE_POINTS_EARNED_CHANGE" => GradeEventKind.AssignedGrade,
        "DRAFT_GRADE_POINTS_EARNED_CHANGE" => GradeEventKind.DraftGrade,
        "MAX_POINTS_CHANGE" => GradeEventKind.MaxPoints,
        _ => GradeEventKind.Other,
    };

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

    // One client for all calls, so connections to Google stay open instead of a new handshake per request.
    // Rebuilt only when the shared credential changes (after connect/disconnect).
    private async Task<ClassroomService> GetClassroomAsync(CancellationToken cancellationToken)
    {
        var credential = await _authorization.GetCredentialAsync(cancellationToken);
        if (_client is { } client && ReferenceEquals(client.Credential, credential))
        {
            return client.Service;
        }

        var service = new ClassroomService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = GoogleAuthorization.ApplicationName,
        });
        _client = new Client(credential, service);
        return service;
    }

    private sealed record Client(UserCredential Credential, ClassroomService Service);
}
