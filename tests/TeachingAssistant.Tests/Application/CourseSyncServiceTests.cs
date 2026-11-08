using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class CourseSyncServiceTests
{
    private static readonly DateTimeOffset Returned = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeSource _source = new();
    private readonly FakeStudentStore _students = new();
    private readonly FakeAssignmentStore _assignments = new();
    private readonly FakeSubmissionStore _submissions = new();
    private readonly FakePhotos _photos = new();

    private Task<CourseSyncSummary> SyncAsync() =>
        new CourseSyncService(_source, _students, _assignments, _submissions, new FakeStorage(), _photos, _photos, TimeProvider.System)
            .SyncAsync("course-1", TestContext.Current.CancellationToken);

    [Fact]
    public async Task Photos_AreDownloadedOnce_AndOnlyAgainWhenTheUrlChanges()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.", "https://photo/ana-1");
        _source.Student("u2", "Gio M."); // no photo: initials

        var first = await SyncAsync();
        var second = await SyncAsync();

        Assert.Equal((1, 0), (first.PhotosUpdated, first.PhotosFailed));
        Assert.Equal(0, second.PhotosUpdated);
        Assert.Equal(["https://photo/ana-1"], _photos.Downloads);
        var ana = _students.Students.Single(x => x.ExternalUserId == "u1");
        Assert.Equal("u1.jpg", ana.PhotoStoredName);
        Assert.NotNull(ana.PhotoHash);
        Assert.Null(_students.Students.Single(x => x.ExternalUserId == "u2").PhotoStoredName);

        // A new photo has a new URL: downloaded and stored again.
        _source.Clear();
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.", "https://photo/ana-2");
        _photos.Bytes["https://photo/ana-2"] = [9, 9, 9];
        var third = await SyncAsync();

        Assert.Equal(1, third.PhotosUpdated);
        Assert.Equal(2, _photos.Saved);
    }

    [Fact]
    public async Task Photos_SameImageBehindANewUrl_IsNotWrittenAgain()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.", "https://photo/a");
        await SyncAsync();

        _source.Clear();
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.", "https://photo/a-resized"); // same bytes (FakePhotos default)
        var summary = await SyncAsync();

        Assert.Equal(0, summary.PhotosUpdated);
        Assert.Equal(1, _photos.Saved);
        Assert.Equal("https://photo/a-resized", _students.Students.Single().PhotoUrl);
    }

    [Fact]
    public async Task Photos_RemovedInTheSource_AreDeleted_AndAFailedDownloadDoesNotFailTheSync()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.", "https://photo/a");
        _source.Student("u2", "Gio M.", "https://photo/broken");
        _photos.Failing.Add("https://photo/broken");

        var first = await SyncAsync();

        Assert.Equal((1, 1), (first.PhotosUpdated, first.PhotosFailed));
        Assert.Null(_students.Students.Single(x => x.ExternalUserId == "u2").PhotoStoredName);

        _source.Clear();
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K."); // photo removed in Google
        _source.Student("u2", "Gio M.");
        await SyncAsync();

        Assert.Equal(["u1.jpg"], _photos.Deleted);
        Assert.Null(_students.Students.Single(x => x.ExternalUserId == "u1").PhotoStoredName);
    }

    [Fact]
    public async Task FirstSync_RecordsRosterAssignmentsAndEveryStudentsGrades()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Assignment("a2", "Assignment 2", 50);
        _source.Student("u1", "Ana K.");
        _source.Student("u2", "Gio M.");
        _source.Submission("s1", "a1", "u1", "RETURNED", 90, Assigned(70, Returned), Assigned(90, Returned.AddDays(4)));
        _source.Submission("s2", "a1", "u2", "CREATED", null);
        _source.Submission("s3", "a2", "u1", "TURNED_IN", null);

        var summary = await SyncAsync();

        Assert.Equal(new CourseSyncSummary(2, 2, 0, 2, 3, 3, 1, 0, 0), summary);
        Assert.All(_students.Students, s => Assert.True(s.IsEnrolledIn("course-1")));
        Assert.Equal([100.0, 50.0], _assignments.Assignments.Select(a => a.MaxPoints!.Value));

        var graded = _submissions.Submissions.Single(s => s.ExternalSubmissionId == "s1");
        Assert.Equal((70.0, 90.0), (graded.InitialGrade!.Value, graded.FinalGrade!.Value));
        Assert.Empty(graded.Versions); // no files are downloaded by a sync
        Assert.Equal("submissions/Java II/Assignment 1/Ana K. (u1)", graded.StorageFolder);
        Assert.Equal("CREATED", _submissions.Submissions.Single(s => s.ExternalSubmissionId == "s2").State);
    }

    [Fact]
    public async Task SecondSync_UpdatesInPlace_WithoutDuplicates()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.");
        _source.Submission("s1", "a1", "u1", "RETURNED", 70, Assigned(70, Returned));
        await SyncAsync();

        // The student fixed the work and the teacher returned it again with a new grade; the name changed too.
        _source.Clear();
        _source.Assignment("a1", "Assignment 1 (Lists)", 100);
        _source.Student("u1", "Ana Kapanadze");
        _source.Submission("s1", "a1", "u1", "RETURNED", 95, Assigned(70, Returned), Assigned(95, Returned.AddDays(3)));
        var summary = await SyncAsync();

        Assert.Equal((0, 0), (summary.NewStudents, summary.NewSubmissions));
        Assert.Equal("Ana Kapanadze", Assert.Single(_students.Students).Name);
        Assert.Equal("Assignment 1 (Lists)", Assert.Single(_assignments.Assignments).Title);
        var submission = Assert.Single(_submissions.Submissions);
        Assert.Equal((70.0, 95.0), (submission.InitialGrade!.Value, submission.FinalGrade!.Value));
    }

    [Fact]
    public async Task StudentWhoLeftTheRoster_IsKeptInactive()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.");
        _source.Student("u2", "Gio M.");
        await SyncAsync();

        _source.Clear();
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.");
        var summary = await SyncAsync();

        Assert.Equal(1, summary.LeftStudents);
        Assert.False(_students.Students.Single(s => s.ExternalUserId == "u2").IsEnrolledIn("course-1"));
        Assert.True(_students.Students.Single(s => s.ExternalUserId == "u1").IsEnrolledIn("course-1"));
    }

    [Fact]
    public async Task WorkForAnAssignmentNotListed_IsSkipped()
    {
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.");
        _source.Submission("s1", "draft-assignment", "u1", "CREATED", null);

        var summary = await SyncAsync();

        Assert.Equal(0, summary.Submissions);
        Assert.Empty(_submissions.Submissions);
    }

    [Fact]
    public async Task FetchedSubmission_KeepsItsVersions_AndGetsTheGrade()
    {
        var fetched = new Submission(SubmissionSource.GoogleClassroom, "course-1", "a1", "u1", "Ana K.", "s1",
            "submissions/old-folder", Returned);
        fetched.AddVersion(VersionStatus.Ready, null, "hash", "submissions/old-folder/v1", Returned, Returned, []);
        _submissions.Submissions.Add(fetched);
        _source.Assignment("a1", "Assignment 1", 100);
        _source.Student("u1", "Ana K.");
        _source.Submission("s1", "a1", "u1", "RETURNED", 88, Assigned(88, Returned));

        await SyncAsync();

        var submission = Assert.Single(_submissions.Submissions);
        Assert.Equal("submissions/old-folder", submission.StorageFolder);
        Assert.Single(submission.Versions);
        Assert.Equal(88, submission.FinalGrade);
    }

    [Fact]
    public async Task StudentsTable_AfterASync_ShowsResultsPerAssignmentAndOverall()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-3);
        _source.Assignment("a1", "Assignment 1", 100, past);
        _source.Assignment("a2", "Assignment 2", 100, past.AddDays(1));
        _source.Assignment("a3", "Assignment 3", 100, DateTimeOffset.UtcNow.AddDays(7)); // not due yet
        _source.Student("u1", "Ana K.");
        _source.Submission("s1", "a1", "u1", "RETURNED", 90, Assigned(70, past), Assigned(90, past.AddDays(1)));
        _source.Submission("s2", "a2", "u1", "CREATED", null); // missing: counts as 0
        _source.Submission("s3", "a3", "u1", "CREATED", null);
        await SyncAsync();

        var table = await new StudentService(_students, _assignments, _submissions, _photos, TimeProvider.System)
            .GetCourseStudentsAsync("course-1", TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a2", "a3"], table.Assignments.Select(a => a.Id));
        var ana = Assert.Single(table.Students);
        Assert.Equal((1, 1, 90.0, 200.0), (ana.TurnedIn, ana.Missing, ana.Earned, ana.Possible));
        Assert.Equal(45, ana.OverallPercent);
        var first = ana.Results[0];
        Assert.Equal((70.0, 90.0), (first.InitialGrade!.Value, first.FinalGrade!.Value));
        Assert.Equal([70.0, 90.0], first.Returns.Select(r => r.Points!.Value));
        Assert.Equal("s1", first.SubmissionId);
        Assert.Equal(1, ana.Improved);
        Assert.Equal("u1", ana.ExternalUserId);
        Assert.True(ana.Results[1].Missing);
        Assert.False(ana.Results[2].Missing);
    }

    private static GradeChange Assigned(double points, DateTimeOffset at) => new(GradeEventKind.AssignedGrade, points, 100, at);

    private sealed class FakeSource : ISubmissionSource
    {
        private readonly List<AssignmentResponse> _assignments = [];
        private readonly List<RosterStudent> _roster = [];
        private readonly List<SubmissionResponse> _submissions = [];

        public void Assignment(string id, string title, double maxPoints, DateTimeOffset? dueAt = null) =>
            _assignments.Add(new AssignmentResponse(id, "course-1", title, null, "ASSIGNMENT", maxPoints, dueAt, null, null, []));

        public void Student(string id, string name, string? photoUrl = null) => _roster.Add(new RosterStudent(id, name, photoUrl));

        public void Submission(string id, string assignmentId, string userId, string state, double? grade, params GradeChange[] history) =>
            _submissions.Add(new SubmissionResponse(id, assignmentId, userId, _roster.FirstOrDefault(r => r.Id == userId)?.Name ?? userId,
                state, false, null, null, [], null, grade, history));

        public void Clear()
        {
            _assignments.Clear();
            _roster.Clear();
            _submissions.Clear();
        }

        public Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CourseResponse(courseId, "Java II", null, "ACTIVE", null));

        public Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AssignmentResponse>>(_assignments.ToList());

        public Task<IReadOnlyList<RosterStudent>> GetStudentsAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RosterStudent>>(_roster.ToList());

        public Task<IReadOnlyList<SubmissionResponse>> GetCourseSubmissionsAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SubmissionResponse>>(_submissions.ToList());

        public Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStudentStore : IStudentStore
    {
        public List<Student> Students { get; } = [];

        public Task<IReadOnlyList<Student>> FindByExternalIdsAsync(
            SubmissionSource source, IReadOnlyCollection<string> externalUserIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Student>>(Students.Where(s => externalUserIds.Contains(s.ExternalUserId)).ToList());

        public Task<IReadOnlyList<Student>> GetEnrolledAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Student>>(Students.Where(s => s.IsEnrolledIn(courseId)).ToList());

        public Task<IReadOnlyList<Student>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Student>>(Students.Where(s => s.Enrollments.Any(e => e.CourseId == courseId)).ToList());

        public void Add(Student student) => Students.Add(student);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAssignmentStore : ICourseAssignmentStore
    {
        public List<CourseAssignment> Assignments { get; } = [];

        public Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CourseAssignment>>(Assignments.Where(a => a.CourseId == courseId).ToList());

        public void Add(CourseAssignment assignment) => Assignments.Add(assignment);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmissionStore : ISubmissionStore
    {
        public List<Submission> Submissions { get; } = [];

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(Submissions.Where(s => s.CourseId == courseId).ToList());

        public void Add(Submission submission) => Submissions.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // Both the photo source and the photo storage: every URL returns the same bytes unless set otherwise.
    private sealed class FakePhotos : IStudentPhotoSource, IStudentPhotoStorage
    {
        public Dictionary<string, byte[]> Bytes { get; } = [];
        public HashSet<string> Failing { get; } = [];
        public List<string> Downloads { get; } = [];
        public List<string> Deleted { get; } = [];
        public int Saved { get; private set; }

        public Task<DownloadedPhoto> DownloadAsync(string photoUrl, CancellationToken cancellationToken = default)
        {
            lock (Downloads) // photos download in parallel
            {
                Downloads.Add(photoUrl);
            }

            return Failing.Contains(photoUrl)
                ? Task.FromException<DownloadedPhoto>(new TeachingAssistant.Application.Exceptions.SubmissionSourceUnavailableException("404"))
                : Task.FromResult(new DownloadedPhoto(Bytes.GetValueOrDefault(photoUrl, [1, 2, 3]), "image/jpeg"));
        }

        public Task<string> SaveAsync(string externalUserId, DownloadedPhoto photo, CancellationToken cancellationToken = default)
        {
            Saved++;
            return Task.FromResult($"{externalUserId}.jpg");
        }

        public Stream? Open(string storedName) => new MemoryStream([1, 2, 3]);

        public void Delete(string storedName) => Deleted.Add(storedName);
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            $"submissions/{courseName}/{assignmentTitle}/{studentName} ({studentId})";

        public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) => throw new NotSupportedException();
        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();
        public Stream? OpenOriginal(string versionPath, string storedName) => throw new NotSupportedException();
        public bool OriginalExists(string versionPath, string storedName) => throw new NotSupportedException();

        public Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<CodeFile> ListCode(string versionPath) => throw new NotSupportedException();
        public Stream? OpenCode(string versionPath, string relativePath) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
