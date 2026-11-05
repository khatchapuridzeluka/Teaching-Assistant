using System.Collections.Concurrent;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class SubmissionFetchServiceTests
{
    private readonly FakeSubmissionSource _source = new();
    private readonly FakeFileSource _files = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeStore _store = new();

    private SubmissionFetchService Service() => new(_source, _files, _storage, _store, TimeProvider.System);

    private Task<FetchSummary> FetchAsync() => Service().FetchAsync("course-1", "assignment-1", TestContext.Current.CancellationToken);

    [Fact]
    public async Task FirstFetch_StoresTurnedInSubmissionAsVersionOne()
    {
        _files.Add("file-1", "homework.zip", "md5-a");
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"));

        var summary = await FetchAsync();

        Assert.Equal(1, summary.NewSubmissions);
        var submission = Assert.Single(_store.Submissions);
        var version = Assert.Single(submission.Versions);
        Assert.Equal(1, version.Number);
        Assert.Equal(VersionStatus.Ready, version.Status);
        var content = Assert.Single(version.Contents);
        Assert.Equal(ContentKind.CodeArchive, content.Kind);
        Assert.Equal("md5-a", content.Fingerprint);
        Assert.Equal("homework.zip", content.StoredName);
        Assert.Equal(ContentStatus.Extracted, content.Status);
        Assert.Equal("code-hash", version.ContentHash);
        Assert.Null(version.ExtractionDetail);
        Assert.Equal("submissions/Java II/Assignment 4/Ana K. (user-sub-1)", submission.StorageFolder);
        Assert.Equal("submissions/Java II/Assignment 4/Ana K. (user-sub-1)/v1", version.StoragePath);
        Assert.Equal(["homework.zip"], _storage.Committed[version.StoragePath]);
    }

    [Fact]
    public async Task SecondFetch_WithoutChanges_DownloadsNothing()
    {
        _files.Add("file-1", "homework.zip", "md5-a");
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"));
        await FetchAsync();
        _files.Downloads = 0;

        var summary = await FetchAsync();

        Assert.Equal(1, summary.Unchanged);
        Assert.Equal(0, _files.Downloads);
        Assert.Single(Assert.Single(_store.Submissions).Versions);
    }

    [Fact]
    public async Task ReplacedFile_BecomesVersionTwo()
    {
        _files.Add("file-1", "homework.zip", "md5-a");
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"));
        await FetchAsync();

        _files.Add("file-1", "homework.zip", "md5-b"); // student replaced the zip
        var summary = await FetchAsync();

        Assert.Equal(1, summary.NewVersions);
        var submission = Assert.Single(_store.Submissions);
        Assert.Equal([1, 2], submission.Versions.Select(v => v.Number));
        Assert.Equal("md5-b", submission.LatestVersion!.Contents.Single().Fingerprint);
    }

    [Fact]
    public async Task NotTurnedIn_IsCountedButNotStored()
    {
        _files.Add("file-1", "draft.zip", "md5-a");
        _source.Add("sub-1", "Ana K.", "CREATED", DriveAttachment("file-1"));

        var summary = await FetchAsync();

        Assert.Equal(1, summary.NotSubmitted);
        Assert.Empty(_store.Submissions);
        Assert.Equal(0, _files.Downloads);
    }

    [Fact]
    public async Task GitHubLink_IsRecordedAsUnsupportedNextToStoredFiles()
    {
        _files.Add("file-1", "report.docx", "md5-a");
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"),
            new SubmissionAttachment(AttachmentKind.Link, "library", "https://github.com/ana/library", null));

        var summary = await FetchAsync();

        var version = Assert.Single(Assert.Single(_store.Submissions).Versions);
        Assert.Equal(VersionStatus.PartiallySupported, version.Status);
        var repository = version.Contents.Single(c => c.Origin == ContentOrigin.GitHub);
        Assert.Equal(ContentKind.Repository, repository.Kind);
        Assert.Equal(ContentStatus.Unsupported, repository.Status);
        Assert.Null(repository.StoredName);
        Assert.Contains(summary.Problems, p => p.Detail.Contains("not fetched yet"));
    }

    [Fact]
    public async Task FailedDownload_RecordsNothingAndDiscardsFiles()
    {
        _files.Add("file-1", "homework.zip", "md5-a");
        _files.FailDownloads = true;
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"));

        var summary = await FetchAsync();

        Assert.Empty(_store.Submissions);
        Assert.Empty(_storage.Committed);
        Assert.Equal(1, _storage.Discarded);
        Assert.Contains(summary.Problems, p => p.Detail.StartsWith("Download failed"));
    }

    [Fact]
    public async Task ManyStudents_AreEachStoredOnceInListOrder()
    {
        for (var i = 1; i <= 20; i++)
        {
            _files.Add($"file-{i}", $"hw-{i}.zip", $"md5-{i}");
            _source.Add($"sub-{i}", $"Student {i:00}", "TURNED_IN", DriveAttachment($"file-{i}"));
        }

        var summary = await FetchAsync();

        Assert.Equal(20, summary.NewSubmissions);
        Assert.Equal(20, _files.Downloads);
        Assert.Equal(20, _storage.Committed.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => $"Student {i:00}"), _store.Submissions.Select(s => s.StudentName));
        Assert.All(_store.Submissions, s => Assert.Equal(1, Assert.Single(s.Versions).Number));
    }

    [Fact]
    public async Task StudentWithManyFiles_StoresAllInAttachmentOrder()
    {
        var attachments = Enumerable.Range(1, 14).Select(i =>
        {
            _files.Add($"file-{i}", $"part-{i}.zip", $"md5-{i}");
            return DriveAttachment($"file-{i}");
        }).ToArray();
        _source.Add("sub-1", "Ana K.", "TURNED_IN", attachments);

        await FetchAsync();

        var version = Assert.Single(Assert.Single(_store.Submissions).Versions);
        Assert.Equal(Enumerable.Range(1, 14).Select(i => $"file-{i}"), version.Contents.Select(c => c.ExternalId));
        Assert.Equal(14, _storage.Committed[version.StoragePath].Count);
        Assert.Equal(14, _files.Downloads);
    }

    [Fact]
    public async Task OneUnreadableFile_SkipsTheWholeStudent()
    {
        _files.Add("file-1", "a.zip", "md5-a");
        _source.Add("sub-1", "Ana K.", "TURNED_IN", DriveAttachment("file-1"), DriveAttachment("missing"));

        var summary = await FetchAsync();

        Assert.Empty(_store.Submissions);
        Assert.Equal(0, _files.Downloads);
        Assert.Contains(summary.Problems, p => p.Detail.StartsWith("Could not read 'missing'"));
    }

    private static SubmissionAttachment DriveAttachment(string fileId) =>
        new(AttachmentKind.DriveFile, fileId, null, fileId);

    private sealed class FakeSubmissionSource : ISubmissionSource
    {
        private readonly List<SubmissionResponse> _submissions = [];

        public void Add(string id, string student, string state, params SubmissionAttachment[] attachments) =>
            _submissions.Add(new SubmissionResponse(id, $"user-{id}", student, state, false,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, attachments, null));

        public Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SubmissionResponse>>(_submissions);

        public Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CourseResponse(courseId, "Java II", null, "ACTIVE", null));

        public Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssignmentResponse(assignmentId, courseId, "Assignment 4", null, "ASSIGNMENT", 100, null, null, null));

        public Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFileSource : IRemoteFileSource
    {
        private readonly Dictionary<string, RemoteFile> _files = [];

        private int _downloads;

        public int Downloads
        {
            get => _downloads;
            set => _downloads = value;
        }

        public bool FailDownloads { get; set; }

        public void Add(string id, string name, string md5) =>
            _files[id] = new RemoteFile(id, name, "application/octet-stream", 3, md5, null, name, "application/octet-stream", true);

        public Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default) =>
            _files.TryGetValue(fileId, out var file)
                ? Task.FromResult(file)
                : Task.FromException<RemoteFile>(new SubmissionSourceNotFoundException($"File not found: {fileId}."));

        public async Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default)
        {
            if (FailDownloads)
                throw new SubmissionSourceUnavailableException("Google Drive returned 500.");
            Interlocked.Increment(ref _downloads); // students are fetched in parallel
            await destination.WriteAsync("abc"u8.ToArray(), cancellationToken);
        }
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        private int _discarded;

        // Writers run in parallel, one per student.
        public ConcurrentDictionary<string, List<string>> Committed { get; } = [];
        public int Discarded => _discarded;

        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            $"submissions/{courseName}/{assignmentTitle}/{studentName} ({studentId})";

        public Stream? OpenOriginal(string versionPath, string storedName) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<CodeFile> ListCode(string versionPath) => throw new NotSupportedException();

        public Stream? OpenCode(string versionPath, string relativePath) => throw new NotSupportedException();

        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) =>
            new Writer(this, $"{submissionFolder}/v{versionNumber}");

        private sealed class Writer(FakeStorage storage, string path) : ISnapshotWriter
        {
            private readonly List<string> _files = [];
            private bool _committed;

            public string RelativePath => path;

            // Pretends every archive and source file extracts cleanly.
            public Task<CodeExtraction> ExtractCodeAsync(IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default)
            {
                var code = files.Where(f => f.Kind is ContentKind.CodeArchive or ContentKind.SourceFile).Select(f => f.StoredName).ToList();
                return Task.FromResult(new CodeExtraction(code.Count > 0 ? "code-hash" : null, code.Count, code, []));
            }

            public async Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default)
            {
                await write(new MemoryStream());
                lock (_files) // a version's files are downloaded in parallel
                    _files.Add(fileName);
                return fileName;
            }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                storage.Committed[path] = _files;
                _committed = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!_committed)
                    Interlocked.Increment(ref storage._discarded);
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeStore : ISubmissionStore
    {
        public List<Submission> Submissions { get; } = [];

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(Submissions.Where(s => s.AssignmentId == assignmentId).ToList());

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => Submissions.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
