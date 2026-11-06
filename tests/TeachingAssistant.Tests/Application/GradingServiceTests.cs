using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class GradingServiceTests
{
    private const string StudentName = "Nino Beridze";

    private readonly FakeStore _store = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeSource _source = new();
    private readonly FakeFiles _files = new();
    private readonly RecordingModel _model = new();

    private GradingService Service() => new(_store, _storage, _source, _files, new FakeWordReader(), _model);

    [Fact]
    public async Task Grade_SendsInstructionsFirst_ThenCodeAndStudentFiles()
    {
        var versionId = _store.AddVersion(
            Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"),
            Content(ContentKind.Document, "report.pdf", "application/pdf"),
            Content(ContentKind.Document, "notes.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            Content(ContentKind.Other, "demo.mp4", "video/mp4"));
        _storage.Code["src/Zoo.java"] = "public class Zoo {\n}";
        _storage.Originals["report.pdf"] = [1, 2, 3];
        _storage.Originals["notes.docx"] = Encoding.UTF8.GetBytes("My report");
        _source.Materials = [new SubmissionAttachment(AttachmentKind.DriveFile, "Homework 2.pdf", null, "file-1")];
        _files.Add("file-1", "Homework 2.pdf", "application/pdf", [9, 9]);

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("The review", result.Review);

        // Instructions first (shared by the class), then the student's code, then the student's PDF.
        var files = _model.Parts!.OfType<FilePart>().ToList();
        Assert.Equal(["Homework 2.pdf", "report.pdf"], files.Select(f => f.Name));
        var instructionsAt = _model.Parts!.IndexOf(files[0]);
        var codeAt = _model.Parts!.FindIndex(p => p is TextPart t && t.Text.Contains("<submission>"));
        Assert.True(instructionsAt < codeAt);

        // Code goes as raw text with its path and line numbers, so the review can point at lines.
        var code = ((TextPart)_model.Parts![codeAt]).Text;
        Assert.Contains("<file path=\"src/Zoo.java\">", code);
        Assert.Contains("   1 | public class Zoo {", code);

        // Word documents go as text; formats the model cannot read are reported, not silently dropped.
        Assert.Equal(
            ["Instructions: Homework 2.pdf", "Code: src/Zoo.java", "Student file: report.pdf", "Student file: notes.docx (as text)"],
            result.SentFiles);
        Assert.Contains(_model.Parts!, p => p is TextPart t && t.Text.Contains("<student_document name=\"notes.docx\">\nMy report"));
        Assert.Contains(result.SkippedFiles, s => s.Contains("demo.mp4"));
    }

    [Fact]
    public async Task Grade_SendsWordInstructionsAsText()
    {
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _source.Materials = [new SubmissionAttachment(AttachmentKind.DriveFile, "Homework_2.docx", null, "file-2")];
        _files.Add("file-2", "Homework_2.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Encoding.UTF8.GetBytes("Task 1: Animal | 15"));

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(["Instructions: Homework_2.docx (as text)", "Code: Main.java"], result!.SentFiles);
        Assert.Contains(_model.Parts!, p => p is TextPart t
            && t.Text.Contains("<instructions_document name=\"Homework_2.docx\">\nTask 1: Animal | 15"));
    }

    [Fact]
    public async Task Grade_NeverSendsTheStudentsName()
    {
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _source.Description = "Write a zoo.";

        await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        var everything = _model.System + string.Concat(_model.Parts!.OfType<TextPart>().Select(t => t.Text));
        Assert.DoesNotContain(StudentName, everything);
    }

    [Fact]
    public async Task Grade_SkipsBinaryFilesInTheCode()
    {
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _storage.Code["diagram.png"] = "\0PNG";
        _source.Description = "Write a zoo.";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(["Code: Main.java"], result!.SentFiles);
        Assert.Contains(result.SkippedFiles, s => s.StartsWith("diagram.png"));
    }

    [Fact]
    public async Task Grade_RejectsAnAssignmentWithoutInstructions()
    {
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken));
        Assert.Null(_model.Parts); // the model was never called
    }

    [Fact]
    public async Task Grade_ReturnsNullForAnUnknownVersion()
    {
        var result = await Service().GradeVersionAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static VersionContent Content(ContentKind kind, string name, string mimeType) =>
        new(ContentOrigin.Drive, kind, $"drive-{name}", name, mimeType, "md5", null, 100, name, null, ContentStatus.Stored);

    private sealed class FakeStore : ISubmissionStore
    {
        private readonly List<Submission> _submissions = [];

        public Guid AddVersion(params VersionContent[] contents)
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1",
                StudentName, "submission-1", "folder", DateTimeOffset.UtcNow);
            var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, contents);
            _submissions.Add(submission);
            return version.Id;
        }

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_submissions.FirstOrDefault(s => s.Versions.Any(v => v.Id == versionId)));

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        public Dictionary<string, string> Code { get; } = [];
        public Dictionary<string, byte[]> Originals { get; } = [];

        public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
            Code.OrderBy(c => c.Key).Select(c => new CodeFile(c.Key, Encoding.UTF8.GetByteCount(c.Value))).ToList();

        public Stream? OpenCode(string versionPath, string relativePath) =>
            Code.TryGetValue(relativePath, out var text) ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null;

        public Stream? OpenOriginal(string versionPath, string storedName) =>
            Originals.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null;

        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            throw new NotSupportedException();

        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSource : ISubmissionSource
    {
        public string? Description { get; set; }
        public IReadOnlyList<SubmissionAttachment> Materials { get; set; } = [];

        public Task<AssignmentResponse> GetAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AssignmentResponse(assignmentId, courseId, "Homework 2", Description, "ASSIGNMENT", 100, null, null, null, Materials));

        public Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CourseResponse> GetCourseAsync(string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SubmissionResponse>> GetSubmissionsAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFiles : IRemoteFileSource
    {
        private readonly Dictionary<string, (RemoteFile File, byte[] Content)> _files = [];

        public void Add(string id, string name, string mimeType, byte[] content) =>
            _files[id] = (new RemoteFile(id, name, mimeType, content.Length, "md5", null, name, mimeType, true), content);

        public Task<RemoteFile> GetFileAsync(string fileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files[fileId].File);

        public Task DownloadAsync(RemoteFile file, Stream destination, CancellationToken cancellationToken = default) =>
            destination.WriteAsync(_files[file.Id].Content, cancellationToken).AsTask();
    }

    // Treats a .docx's bytes as its text; the real format is tested in WordDocumentReaderTests.
    private sealed class FakeWordReader : IDocumentReader
    {
        public bool CanRead(string fileName, string? mediaType) => fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

        public string? ReadText(byte[] content) => Encoding.UTF8.GetString(content);
    }

    private sealed class RecordingModel : ILanguageModel
    {
        public string? System { get; private set; }
        public List<PromptPart>? Parts { get; private set; }

        public string ModelId => "fake-model";

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, CancellationToken cancellationToken = default)
        {
            System = systemPrompt;
            Parts = parts.ToList();
            return Task.FromResult(new LanguageModelReply("fake-grader", "The review"));
        }

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
