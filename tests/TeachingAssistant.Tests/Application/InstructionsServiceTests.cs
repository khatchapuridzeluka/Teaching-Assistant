using System.Text;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class InstructionsServiceTests
{
    private const string AssignmentId = "assignment-1";

    private readonly FakeStore _store = new();
    private readonly FakeStorage _storage = new();
    private readonly InstructionsService _service;

    public InstructionsServiceTests()
    {
        _service = new InstructionsService(_store, _storage);
    }

    [Fact]
    public async Task GetLatestAsync_returns_null_before_the_first_fetch()
    {
        Assert.Null(await _service.GetLatestAsync(AssignmentId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLatestAsync_returns_the_newest_version_with_its_files_and_link_urls()
    {
        var instructions = NewInstructions();
        instructions.AddVersion("Homework", "old text", 100, "v1", DateTimeOffset.UtcNow, [Pdf("old.pdf")]);
        instructions.AddVersion("Homework", "new text", 100, "v2", DateTimeOffset.UtcNow,
        [
            Pdf("task.pdf"),
            new InstructionsContent(ContentOrigin.Link, "https://example.com/spec", "Spec", null, "link", null, null, ContentStatus.Unsupported),
        ]);
        _store.Instructions = instructions;

        var latest = await _service.GetLatestAsync(AssignmentId, TestContext.Current.CancellationToken);

        Assert.NotNull(latest);
        Assert.Equal(2, latest.Number);
        Assert.Equal(2, latest.VersionCount);
        Assert.Equal("new text", latest.Description);
        Assert.Collection(latest.Files,
            pdf =>
            {
                Assert.Equal("task.pdf", pdf.Name);
                Assert.True(pdf.Stored);
                Assert.Null(pdf.Url);
            },
            link =>
            {
                Assert.False(link.Stored);
                Assert.Equal("https://example.com/spec", link.Url);
            });
    }

    [Fact]
    public async Task OpenFileAsync_opens_a_stored_file_of_the_assignment_only()
    {
        var instructions = NewInstructions();
        var pdf = Pdf("task.pdf");
        instructions.AddVersion("Homework", null, 100, "v1", DateTimeOffset.UtcNow, [pdf]);
        _store.Instructions = instructions;
        _storage.Originals["task.pdf"] = Encoding.UTF8.GetBytes("%PDF");

        var file = await _service.OpenFileAsync(AssignmentId, pdf.Id, TestContext.Current.CancellationToken);
        var otherAssignment = await _service.OpenFileAsync("assignment-2", pdf.Id, TestContext.Current.CancellationToken);
        var unknown = await _service.OpenFileAsync(AssignmentId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(file);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("task.pdf", file.FileName);
        Assert.Null(otherAssignment);
        Assert.Null(unknown);
    }

    private static AssignmentInstructions NewInstructions() =>
        new(SubmissionSource.GoogleClassroom, "course-1", AssignmentId, "instructions-folder", DateTimeOffset.UtcNow);

    private static InstructionsContent Pdf(string name) =>
        new(ContentOrigin.Drive, $"drive-{name}", name, "application/pdf", "md5", 4, name, ContentStatus.Stored);

    private sealed class FakeStore : IInstructionsStore
    {
        public AssignmentInstructions? Instructions { get; set; }

        public Task<AssignmentInstructions?> GetAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Instructions?.AssignmentId == assignmentId ? Instructions : null);

        public void Add(AssignmentInstructions instructions) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        public Dictionary<string, byte[]> Originals { get; } = [];

        public bool OriginalExists(string versionPath, string storedName) => throw new NotSupportedException();

        public Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Stream? OpenOriginal(string versionPath, string storedName) =>
            Originals.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null;

        public IReadOnlyList<CodeFile> ListCode(string versionPath) => throw new NotSupportedException();

        public Stream? OpenCode(string versionPath, string relativePath) => throw new NotSupportedException();

        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            throw new NotSupportedException();

        public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) =>
            throw new NotSupportedException();

        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
