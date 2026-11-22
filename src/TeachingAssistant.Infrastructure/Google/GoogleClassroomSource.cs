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

public class GoogleClassroomSource : ISubmissionSource
{
    private const string ServiceName = "Google Classroom";

    private const int PageSize = 100;

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
        request.TeacherId = "me";
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

    private static CourseResponse ToCourse(Course c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Section = c.Section,
        State = c.CourseState,
        Link = c.AlternateLink,
    };

    private static AssignmentResponse ToAssignment(CourseWork w) => new()
    {
        Id = w.Id,
        CourseId = w.CourseId,
        Title = w.Title,
        Description = w.Description,
        WorkType = w.WorkType,
        MaxPoints = w.MaxPoints,
        DueAt = DueAt(w.DueDate, w.DueTime),
        CreatedAt = w.CreationTimeDateTimeOffset,
        Link = w.AlternateLink,
        Materials = Materials(w),
    };

    private static List<SubmissionAttachment> Materials(CourseWork courseWork) =>
        (courseWork.Materials ?? []).Select(m => m switch
        {
            { DriveFile.DriveFile: { } file } => new SubmissionAttachment
            {
                Kind = AttachmentKind.DriveFile,
                Title = file.Title,
                Url = file.AlternateLink,
                FileId = file.Id,
            },
            { Link: { } link } => new SubmissionAttachment
            {
                Kind = AttachmentKind.Link,
                Title = link.Title ?? link.Url,
                Url = link.Url,
            },
            _ => new SubmissionAttachment
            {
                Kind = AttachmentKind.Other,
                Title = m.YoutubeVideo?.Title ?? m.Form?.Title ?? "Unsupported material",
            },
        }).ToList();

    public async Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

        var namesCall = GetStudentNamesAsync(classroom, courseId, cancellationToken);

        var submissionsCall = ListSubmissionsAsync(classroom, courseId, assignmentId, cancellationToken);

        await Task.WhenAll(namesCall, submissionsCall);
        var (names, submissions) = (await namesCall, await submissionsCall);

        return submissions.Select(s => ToSubmission(s, names)).ToList();
    }

    public async Task<IReadOnlyList<SubmissionResponse>> GetCourseSubmissionsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        var classroom = await GetClassroomAsync(cancellationToken);

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
            .Select(s => new RosterStudent
            {
                Id = s.UserId,
                Name = s.Profile?.Name?.FullName ?? s.UserId,
                PhotoUrl = PhotoUrl(s.Profile?.PhotoUrl),
            })
            .ToList();
    }

    private static string? PhotoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains("default-user", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var absolute = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;

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

    private static SubmissionResponse ToSubmission(StudentSubmission s, Dictionary<string, string> names) => new()
    {
        Id = s.Id,
        AssignmentId = s.CourseWorkId,
        StudentId = s.UserId,
        StudentName = names.GetValueOrDefault(s.UserId, "Unknown student"),
        State = s.State,
        Late = s.Late ?? false,
        TurnedInAt = LastTurnedInAt(s),
        UpdatedAt = s.UpdateTimeDateTimeOffset,
        Attachments = Attachments(s),
        Link = s.AlternateLink,
        AssignedGrade = s.AssignedGrade,
        GradeHistory = GradeHistory(s),
    };

    private static List<GradeChange> GradeHistory(StudentSubmission submission) =>
        (submission.SubmissionHistory ?? [])
            .Select(h => h.GradeHistory)
            .Where(g => g?.GradeTimestampDateTimeOffset is not null)
            .Select(g => new GradeChange
            {
                Kind = GradeKind(g!.GradeChangeType),
                PointsEarned = g.PointsEarned,
                MaxPoints = g.MaxPoints,
                At = g.GradeTimestampDateTimeOffset!.Value,
            })
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

    private static Task<IList<TItem>> FetchAllAsync<TRequest, TResponse, TItem>(
        TRequest request,
        Action<TRequest, string?> setPageToken,
        Func<TResponse, (IList<TItem>? Items, string? NextPageToken)> readPage,
        CancellationToken cancellationToken)
        where TRequest : IClientServiceRequest<TResponse>
    {
        var streamer = new PageStreamer<TItem, TRequest, TResponse, string?>(
            setPageToken,
            page => readPage(page).NextPageToken is { Length: > 0 } token ? token : null,
            page => readPage(page).Items ?? []);

        return GoogleErrors.TranslateAsync(ServiceName, () => streamer.FetchAllAsync(request, cancellationToken));
    }

    private static DateTimeOffset? LastTurnedInAt(StudentSubmission submission) =>
        (submission.SubmissionHistory ?? [])
            .Select(h => h.StateHistory)
            .Where(h => h?.State == "TURNED_IN")
            .Max(h => h!.StateTimestampDateTimeOffset);

    private static List<SubmissionAttachment> Attachments(StudentSubmission submission) =>
        (submission.AssignmentSubmission?.Attachments ?? []).Select(a => a switch
        {
            { DriveFile: { } file } => new SubmissionAttachment
            {
                Kind = AttachmentKind.DriveFile,
                Title = file.Title,
                Url = file.AlternateLink,
                FileId = file.Id,
            },
            { Link: { } link } => new SubmissionAttachment
            {
                Kind = AttachmentKind.Link,
                Title = link.Title ?? link.Url,
                Url = link.Url,
            },
            _ => new SubmissionAttachment
            {
                Kind = AttachmentKind.Other,
                Title = a.YouTubeVideo?.Title ?? a.Form?.Title ?? "Unsupported attachment",
            },
        }).ToList();

    private static DateTimeOffset? DueAt(Date? date, TimeOfDay? time) =>
        date is { Year: { } year, Month: { } month, Day: { } day }
            ? new DateTimeOffset(year, month, day, time?.Hours ?? 0, time?.Minutes ?? 0, 0, TimeSpan.Zero)
            : null;

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
        _client = new Client { Credential = credential, Service = service };
        return service;
    }

    private sealed record Client
    {
        public required UserCredential Credential { get; init; }
        public required ClassroomService Service { get; init; }
    }
}
