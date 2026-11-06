using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class InstructionsFetchServiceTests
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly FakeStore _store = new();
    private readonly FakeFiles _files = new();
    private readonly FakeStorage _storage = new();

    private InstructionsFetchService Service() => new(_store, _files, _storage, new FakeWordReader(), TimeProvider.System);

    private Task<InstructionsFetchResult> FetchAsync(AssignmentResponse assignment) =>
        Service().FetchAsync("course-1", "Java II", assignment, TestContext.Current.CancellationToken);

    [Fact]
    public async Task FirstFetch_StoresVersionOne_DownloadingOnlyWhatTheGraderCanRead()
    {
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-a");
        _files.Add("video-1", "addFirst.mp4", "video/mp4", "md5-v");
        var assignment = Assignment(Drive("doc-1"), Drive("video-1"),
            new SubmissionAttachment(AttachmentKind.Link, "Java docs", "https://docs.oracle.com", null));

        var result = await FetchAsync(assignment);

        Assert.Equal(1, result.VersionNumber);
        Assert.True(result.IsNew);
        var version = Assert.Single(_store.Saved!.Versions);
        Assert.Equal("instructions/Java II/Homework 2/v1", version.StoragePath);
        Assert.Equal("Write a zoo.", version.Description);
        Assert.Equal(["Homework_2.docx"], _storage.Stored);

        var doc = version.Contents.Single(c => c.Name == "Homework_2.docx");
        Assert.Equal((ContentStatus.Stored, "Homework_2.docx"), (doc.Status, doc.StoredName));
        var video = version.Contents.Single(c => c.Name == "addFirst.mp4");
        Assert.Equal((ContentStatus.Unsupported, (string?)null), (video.Status, video.StoredName));
        var link = version.Contents.Single(c => c.Name == "Java docs");
        Assert.Equal((ContentOrigin.Link, ContentStatus.Unsupported), (link.Origin, link.Status));
    }

    [Fact]
    public async Task UnchangedInstructions_AreNotDownloadedAgain()
    {
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-a");
        await FetchAsync(Assignment(Drive("doc-1")));

        var second = await FetchAsync(Assignment(Drive("doc-1")));

        Assert.Equal((1, false), (second.VersionNumber, second.IsNew));
        Assert.Single(_store.Saved!.Versions);
        Assert.Equal(1, _files.Downloads);
    }

    [Fact]
    public async Task ReplacedFile_BecomesVersionTwo()
    {
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-a");
        await FetchAsync(Assignment(Drive("doc-1")));
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-b"); // the teacher re-uploaded it

        var second = await FetchAsync(Assignment(Drive("doc-1")));

        Assert.Equal((2, true), (second.VersionNumber, second.IsNew));
        Assert.Equal("md5-b", _store.Saved!.LatestVersion!.Contents.Single().Fingerprint);
    }

    [Fact]
    public async Task ChangedTaskStatement_BecomesVersionTwo()
    {
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-a");
        await FetchAsync(Assignment(Drive("doc-1")));

        var second = await FetchAsync(Assignment(Drive("doc-1")) with { Description = "Write a bigger zoo." });

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal("Write a bigger zoo.", _store.Saved!.LatestVersion!.Description);
    }

    [Fact]
    public async Task TooLargeFile_IsRecordedAndReported_ButNotStored()
    {
        _files.Add("doc-1", "Homework_2.docx", DocxType, "md5-a", sizeBytes: 50L * 1024 * 1024);

        var result = await FetchAsync(Assignment(Drive("doc-1")));

        var content = Assert.Single(_store.Saved!.LatestVersion!.Contents);
        Assert.Equal(ContentStatus.TooLarge, content.Status);
        Assert.Empty(_storage.Stored);
        Assert.Contains(result.Problems, p => p.Contains("larger than"));
    }

    private static AssignmentResponse Assignment(params SubmissionAttachment[] materials) =>
        new("assignment-1", "course-1", "Homework 2", "Write a zoo.", "ASSIGNMENT", 100, null, null, null, materials);

    private static SubmissionAttachment Drive(string fileId) => new(AttachmentKind.DriveFile, fileId, null, fileId);

    private sealed class FakeStore : IInstructionsStore
    {
        public AssignmentInstructions? Saved { get; private set; }

        public Task<AssignmentInstructions?> GetAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved);

        public void Add(AssignmentInstructions instructions) => Saved = instructions;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiles : IRemoteFileSource
    {
        private readonly Dictionary<string, RemoteFile> _files = [];

        public int Downloads { get; private set; }

        public void Add(string id, string name, string mimeType, string md5, long sizeBytes = 1000) =>
            _files[id] = new RemoteFile(id, name, mimeType, sizeBytes, md5, null, name, mimeType, true);

        public Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files[fileId]);

        public Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default)
        {
            Downloads++;
            return destination.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken).AsTask();
        }
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        public List<string> Stored { get; } = [];

        public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) =>
            $"instructions/{courseName}/{assignmentTitle}";

        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) =>
            new Writer(this, $"{submissionFolder}/v{versionNumber}");

        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            throw new NotSupportedException();

        public Stream? OpenOriginal(string versionPath, string storedName) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<CodeFile> ListCode(string versionPath) => throw new NotSupportedException();

        public Stream? OpenCode(string versionPath, string relativePath) => throw new NotSupportedException();

        private sealed class Writer(FakeStorage storage, string path) : ISnapshotWriter
        {
            public string RelativePath => path;

            public async Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default)
            {
                await write(new MemoryStream());
                storage.Stored.Add(fileName);
                return fileName;
            }

            public Task<CodeExtraction> ExtractCodeAsync(IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWordReader : IDocumentReader
    {
        public bool CanRead(string fileName, string? mediaType) => fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

        public ReadDocument? Read(byte[] content) => new("text", []);
    }
}
