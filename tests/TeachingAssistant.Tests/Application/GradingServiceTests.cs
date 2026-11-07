using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class GradingServiceTests
{
    private const string StudentName = "Nino Beridze";
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly FakeStore _store = new();
    private readonly FakeInstructionsStore _instructions = new();
    private readonly FakeStorage _storage = new();
    private readonly RecordingModel _model = new();
    private readonly FakeWordReader _wordReader = new();

    private GradingService Service() => new(_store, _instructions, _storage, _wordReader, _model);

    [Fact]
    public async Task Grade_SendsStoredInstructionsFirst_ThenCodeAndStudentFiles()
    {
        _instructions.Store(null,
            Stored("Homework 2.pdf", "application/pdf", [9, 9]),
            new InstructionsContent(ContentOrigin.Drive, "video-1", "demo.mp4", "video/mp4", "md5", 1000, null, ContentStatus.Unsupported));
        var versionId = _store.AddVersion(
            Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"),
            Content(ContentKind.Document, "report.pdf", "application/pdf"),
            Content(ContentKind.Document, "notes.docx", DocxType),
            Content(ContentKind.Other, "screen.mp4", "video/mp4"));
        _storage.Code["src/Zoo.java"] = "public class Zoo {\n}";
        _storage.Originals["report.pdf"] = [1, 2, 3];
        _storage.Originals["notes.docx"] = Encoding.UTF8.GetBytes("My report");

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("The review", result.Review);
        Assert.Equal(1, result.InstructionsVersion);

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

        // Word documents go as text; what the model cannot read is reported, not silently dropped.
        Assert.Equal(
            ["Instructions: Homework 2.pdf", "Code: src/Zoo.java", "Student file: report.pdf", "Student file: notes.docx (as text)"],
            result.SentFiles);
        Assert.Contains(_model.Parts!, p => p is TextPart t && t.Text.Contains("<student_document name=\"notes.docx\">\nMy report"));
        Assert.Contains(result.SkippedFiles, s => s.Contains("demo.mp4"));
        Assert.Contains(result.SkippedFiles, s => s.Contains("screen.mp4"));
    }

    [Fact]
    public async Task Grade_ReturnsMistakes_WithFilesAndLinesCheckedAgainstTheSentCode()
    {
        _instructions.Store("Write a zoo.");
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["src/Zoo.java"] = "public class Zoo {\n    public int size;\n    void feed() {}\n}";
        _storage.Code["src/Main.java"] = "class Main {}";
        _model.Reply =
            """
            {
              "review": "## Score\n90/100",
              "mistakes": [
                { "file": "src/Zoo.java", "startLine": 2, "endLine": 2, "severity": "Major", "category": "Encapsulation", "message": " Field is public. " },
                { "file": "Zoo.java", "startLine": 3, "endLine": 99, "severity": "Minor", "category": "Naming", "message": "Unclear name." },
                { "file": "src/Zoo.java", "startLine": 40, "endLine": 41, "severity": "Style", "category": "Readability", "message": "Lines that do not exist." },
                { "file": "Other.java", "startLine": 1, "endLine": 1, "severity": "Major", "category": "Correctness", "message": "A file that was not sent." },
                { "file": null, "startLine": null, "endLine": null, "severity": "Major", "category": "MissingRequirement", "message": "Task 3 is missing." }
              ]
            }
            """;

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Contains("\"enum\"", _model.Schema);
        Assert.Equal("## Score\n90/100", result!.Review);
        Assert.Equal(
        [
            // As given, trimmed.
            new CodeMistake("src/Zoo.java", 2, 2, MistakeSeverity.Major, MistakeCategory.Encapsulation, "Field is public."),
            // The file name alone matches the one file with that name; the end line is kept inside the file (4 lines).
            new CodeMistake("src/Zoo.java", 3, 4, MistakeSeverity.Minor, MistakeCategory.Naming, "Unclear name."),
            // Lines outside the file are dropped, the file is kept.
            new CodeMistake("src/Zoo.java", null, null, MistakeSeverity.Style, MistakeCategory.Readability, "Lines that do not exist."),
            // A file that was not sent is dropped with its lines: the mistake stays, about the submission as a whole.
            new CodeMistake(null, null, null, MistakeSeverity.Major, MistakeCategory.Correctness, "A file that was not sent."),
            new CodeMistake(null, null, null, MistakeSeverity.Major, MistakeCategory.MissingRequirement, "Task 3 is missing."),
        ], result.Mistakes);
    }

    [Fact]
    public async Task Grade_ShowsAReplyThatIsNotTheExpectedJson_AsTheReview()
    {
        _instructions.Store("Write a zoo.");
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _model.Reply = "Plain text review";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal("Plain text review", result!.Review);
        Assert.Empty(result.Mistakes);
    }

    [Fact]
    public async Task Grade_SendsStoredWordInstructionsAsText()
    {
        _instructions.Store(null, Stored("Homework_2.docx", DocxType, Encoding.UTF8.GetBytes("Task 1: Animal | 15")));
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(["Instructions: Homework_2.docx (as text)", "Code: Main.java"], result!.SentFiles);
        Assert.Contains(_model.Parts!, p => p is TextPart t
            && t.Text.Contains("<instructions_document name=\"Homework_2.docx\">\nTask 1: Animal | 15"));
    }

    [Fact]
    public async Task Grade_SendsThePicturesInWordInstructions_RightAfterTheirText()
    {
        // Instructions often paste the code as IDE screenshots; without them the model cannot check the drawings.
        _instructions.Store(null, Stored("Homework_1.docx", DocxType, Encoding.UTF8.GetBytes("Task 1\n[image 1]\nTask 2\n[image 2]")));
        _wordReader.Images.Add(new DocumentImage("image1.png", "image/png", [1]));
        _wordReader.Images.Add(new DocumentImage("image2.png", "image/png", [2]));
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "hw.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        var parts = _model.Parts!;
        var instructionsAt = parts.FindIndex(p => p is TextPart t && t.Text.StartsWith("<instructions_document"));
        var codeAt = parts.FindIndex(p => p is TextPart t && t.Text.Contains("<submission>"));
        var images = parts.OfType<FilePart>().ToList();
        Assert.Equal(["Homework_1.docx [image 1]", "Homework_1.docx [image 2]"], images.Select(i => i.Name));
        Assert.All(images, image => Assert.InRange(parts.IndexOf(image), instructionsAt + 1, codeAt - 1));
        Assert.Contains(parts, p => p is TextPart { Text: "[image 2] in Homework_1.docx:" });
        Assert.Equal(["Instructions: Homework_1.docx (as text, 2 images)", "Code: Main.java"], result!.SentFiles);
    }

    [Fact]
    public async Task Grade_NeverSendsTheStudentsName()
    {
        _instructions.Store("Write a zoo.");
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        var everything = _model.System + string.Concat(_model.Parts!.OfType<TextPart>().Select(t => t.Text));
        Assert.DoesNotContain(StudentName, everything);
    }

    [Fact]
    public async Task Grade_SkipsBinaryFilesInTheCode_ThatAreNotImages()
    {
        _instructions.Store("Write a zoo.");
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _storage.Code["data.bin"] = "\0\u0001\u0002";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        Assert.Equal(["Code: Main.java"], result!.SentFiles);
        Assert.Contains(result.SkippedFiles, s => s.StartsWith("data.bin"));
    }

    [Fact]
    public async Task Grade_SendsImagesFromTheArchive_AsImagesAfterTheCode()
    {
        // Homework often asks for drawings (stack and heap diagrams) zipped together with the code.
        _instructions.Store("Draw the stack and the heap.");
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "hw.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";
        _storage.Code["drawings/Task 1.png"] = "\0PNG";
        _storage.Code["drawings/Task 2.JPG"] = "\0JPG";

        var result = await Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken);

        var images = _model.Parts!.OfType<FilePart>().ToList();
        Assert.Equal([("drawings/Task 1.png", "image/png"), ("drawings/Task 2.JPG", "image/jpeg")],
            images.Select(i => (i.Name, i.MediaType)));
        var codeAt = _model.Parts!.FindIndex(p => p is TextPart t && t.Text.Contains("<submission>"));
        Assert.True(codeAt < _model.Parts!.IndexOf(images[0]));
        Assert.Contains(_model.Parts!, p => p is TextPart { Text: "Image in the student's submission: drawings/Task 1.png" });
        Assert.Equal(["Code: Main.java", "Image: drawings/Task 1.png", "Image: drawings/Task 2.JPG"], result!.SentFiles);
    }

    [Fact]
    public async Task Grade_AsksToFetchFirst_WhenNoInstructionsAreStored()
    {
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        var error = await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken));

        Assert.Contains("Fetch from Classroom", error.Message);
        Assert.Null(_model.Parts); // the model was never called
    }

    [Fact]
    public async Task Grade_Rejects_WhenTheStoredInstructionsHaveNothingReadable()
    {
        _instructions.Store(null,
            new InstructionsContent(ContentOrigin.Drive, "video-1", "demo.mp4", "video/mp4", "md5", 1000, null, ContentStatus.Unsupported));
        var versionId = _store.AddVersion(Content(ContentKind.CodeArchive, "zoo.zip", "application/zip"));
        _storage.Code["Main.java"] = "class Main {}";

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().GradeVersionAsync(versionId, TestContext.Current.CancellationToken));
        Assert.Null(_model.Parts);
    }

    [Fact]
    public async Task Grade_ReturnsNullForAnUnknownVersion()
    {
        var result = await Service().GradeVersionAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static VersionContent Content(ContentKind kind, string name, string mimeType) =>
        new(ContentOrigin.Drive, kind, $"drive-{name}", name, mimeType, "md5", null, 100, name, null, ContentStatus.Stored);

    // A stored instruction file; its bytes are registered with the fake storage by FakeInstructionsStore.Store.
    private InstructionsContent Stored(string name, string mimeType, byte[] bytes)
    {
        _storage.Originals[name] = bytes;
        return new InstructionsContent(ContentOrigin.Drive, $"drive-{name}", name, mimeType, "md5", bytes.Length, name, ContentStatus.Stored);
    }

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

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeInstructionsStore : IInstructionsStore
    {
        private AssignmentInstructions? _instructions;

        public void Store(string? description, params InstructionsContent[] contents)
        {
            _instructions = new AssignmentInstructions(SubmissionSource.GoogleClassroom, "course-1", "assignment-1",
                "instructions-folder", DateTimeOffset.UtcNow);
            _instructions.AddVersion("Homework 2", description, 100, "instructions-folder/v1", DateTimeOffset.UtcNow, contents);
        }

        public Task<AssignmentInstructions?> GetAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_instructions?.AssignmentId == assignmentId ? _instructions : null);

        public void Add(AssignmentInstructions instructions) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : ISnapshotStorage
    {
        public Dictionary<string, string> Code { get; } = [];

        // Stored originals by name, for both instruction files and student files.
        public Dictionary<string, byte[]> Originals { get; } = [];

        public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
            Code.OrderBy(c => c.Key).Select(c => new CodeFile(c.Key, Encoding.UTF8.GetByteCount(c.Value))).ToList();

        public Stream? OpenCode(string versionPath, string relativePath) =>
            Code.TryGetValue(relativePath, out var text) ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null;

        public bool OriginalExists(string versionPath, string storedName) => throw new NotSupportedException();

        public Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Stream? OpenOriginal(string versionPath, string storedName) =>
            Originals.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null;

        public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
            throw new NotSupportedException();

        public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) =>
            throw new NotSupportedException();

        public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();

        public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // Treats a .docx's bytes as its text and returns the configured images; the real format is tested in
    // WordDocumentReaderTests.
    private sealed class FakeWordReader : IDocumentReader
    {
        public List<DocumentImage> Images { get; } = [];

        public bool CanRead(string fileName, string? mediaType) => fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

        public ReadDocument? Read(byte[] content) => new(Encoding.UTF8.GetString(content), Images);
    }

    private sealed class RecordingModel : ILanguageModel
    {
        public string? System { get; private set; }
        public List<PromptPart>? Parts { get; private set; }

        public string? Schema { get; private set; }

        // What the model answers: structured output, as the real API returns it.
        public string Reply { get; set; } = """{ "review": "The review", "mistakes": [] }""";

        public string ModelId => "fake-model";

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            System = systemPrompt;
            Parts = parts.ToList();
            Schema = outputSchema;
            return Task.FromResult(new LanguageModelReply("fake-grader", Reply));
        }

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
