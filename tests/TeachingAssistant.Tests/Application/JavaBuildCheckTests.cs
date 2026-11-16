using System.Text;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class JavaBuildCheckTests
{
    private readonly FakeSandbox _sandbox = new();
    private readonly FakeCodeStorage _storage = new();

    private JavaBuildCheck Check() => new(_sandbox, _storage);

    private static SubmissionVersion Version()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "assignment-1", "student-1", "Student",
            "submission-1", "folder", DateTimeOffset.UtcNow);
        return submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
    }

    [Fact]
    public async Task NoJavaFiles_SkipsTheCheck_WithoutStartingTheSandbox()
    {
        _storage.Files["notes.txt"] = Encoding.UTF8.GetBytes("hello");

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.Skipped, outcome.Status);
        Assert.Null(_sandbox.Request);
        Assert.Null(JavaBuildCheck.PromptSection(outcome));
    }

    [Fact]
    public async Task CompilesEachProgramOnItsOwn_ThroughAnArgumentFile_WithoutTheByteOrderMark()
    {
        _storage.Files["task1/Main.java"] = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("public class Main {}")];
        _storage.Files["task2/Main.java"] = Encoding.UTF8.GetBytes("public class Main {}");
        _sandbox.Result = Steps(Succeeded("compile task1/"), Succeeded("compile task2/"));

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        var request = _sandbox.Request!;
        Assert.Equal(JavaBuildCheck.Image, request.Image);
        Assert.Equal(["compile task1/", "compile task2/"], request.Steps.Select(s => s.Name));
        Assert.Equal(["javac", "-encoding", "UTF-8", JavaBuildCheck.Warnings, "-Xmaxerrs", "50", "-Xmaxwarns", "50", "-d", "/tmp/classes-1",
                "@__ta_sources_1.txt"],
            request.Steps[0].Command);
        Assert.Equal("\"task1/Main.java\"\n", Text(request, "__ta_sources_1.txt"));
        Assert.Equal("public class Main {}", Text(request, "task1/Main.java"));
        Assert.Equal((BuildStatus.Compiled, 2), (outcome.Status, outcome.Programs));
    }

    [Fact]
    public async Task ACompileError_IsCompileFailed_WithTheCompilersMessagesForTheGrader()
    {
        _storage.Files["Main.java"] = Encoding.UTF8.GetBytes("class Main { int x = \"a\"; }");
        _sandbox.Result = Steps(new SandboxStepResult("compile", SandboxStepOutcome.Failed, 1, "",
            "Main.java:1: error: incompatible types: String cannot be converted to int\n1 error\n", false, TimeSpan.FromSeconds(1)));

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal(BuildStatus.CompileFailed, outcome.Status);
        Assert.Equal("Main.java:1: error: incompatible types: String cannot be converted to int\n1 error", outcome.Output);
        var section = JavaBuildCheck.PromptSection(outcome)!;
        Assert.StartsWith("<build_and_test_results>\nThe tool compiled the submission's Java files with javac (JDK 27)", section);
        Assert.Contains("Result: it did NOT compile.\nCompiler output (paths as in the submission):\nMain.java:1: error:", section);
        Assert.EndsWith("</build_and_test_results>", section);
    }

    [Fact]
    public async Task Warnings_AreCounted_AndATimeoutIsReportedAsUnknown()
    {
        _storage.Files["Main.java"] = Encoding.UTF8.GetBytes("class Main {}");
        _sandbox.Result = Steps(new SandboxStepResult("compile", SandboxStepOutcome.Succeeded, 0, "",
            "Main.java:3: warning: [unchecked] unchecked call\nMain.java:5: warning: [deprecation] old\n2 warnings\n", false, TimeSpan.Zero));

        var compiled = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);
        Assert.Equal((BuildStatus.Compiled, 2), (compiled.Status, compiled.Warnings));
        Assert.Contains("Result: it compiled without errors, with 2 compiler warnings.", JavaBuildCheck.PromptSection(compiled));

        _sandbox.Result = Steps(new SandboxStepResult("compile", SandboxStepOutcome.TimedOut, null, "", "", false, TimeSpan.FromSeconds(30)));
        var timedOut = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);
        Assert.Equal(BuildStatus.TimedOut, timedOut.Status);
        Assert.Contains("was stopped, so whether it compiles is unknown", JavaBuildCheck.PromptSection(timedOut));
    }

    [Fact]
    public async Task AnUnavailableSandbox_IsRecorded_ButLeavesThePromptAlone()
    {
        _storage.Files["Main.java"] = Encoding.UTF8.GetBytes("class Main {}");
        _sandbox.Unavailable = "Docker is not running. Start Docker Desktop and try again.";

        var outcome = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);

        Assert.Equal((BuildStatus.Unavailable, "Docker is not running. Start Docker Desktop and try again."), (outcome.Status, outcome.Output));
        Assert.Null(JavaBuildCheck.PromptSection(outcome));
    }

    private static SandboxResult Steps(params SandboxStepResult[] steps) => new(steps, TimeSpan.FromSeconds(2));

    private static SandboxStepResult Succeeded(string name) =>
        new(name, SandboxStepOutcome.Succeeded, 0, "", "", false, TimeSpan.FromSeconds(1));

    private static string Text(SandboxRequest request, string path) =>
        Encoding.UTF8.GetString(request.Files.Single(f => f.Path == path).Content);
}

/// <summary>A sandbox that records the request and answers with a scripted result, or is unavailable.</summary>
internal sealed class FakeSandbox : ISandboxRunner
{
    public SandboxRequest? Request { get; private set; }

    public SandboxResult? Result { get; set; }

    public string? Unavailable { get; set; }

    public Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default)
    {
        Request = request;
        if (Unavailable is not null)
        {
            throw new SandboxUnavailableException(Unavailable);
        }

        return Task.FromResult(Result ?? new SandboxResult(
            request.Steps.Select(s => new SandboxStepResult(s.Name, SandboxStepOutcome.Succeeded, 0, "", "", false, TimeSpan.Zero)).ToList(),
            TimeSpan.Zero));
    }
}

/// <summary>Extracted code by path; everything else a snapshot storage does is not used here.</summary>
internal sealed class FakeCodeStorage : ISnapshotStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public IReadOnlyList<CodeFile> ListCode(string versionPath) =>
        Files.OrderBy(f => f.Key).Select(f => new CodeFile(f.Key, f.Value.Length)).ToList();

    public Stream? OpenCode(string versionPath, string relativePath) =>
        Files.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes) : null;

    public bool OriginalExists(string versionPath, string storedName) => throw new NotSupportedException();

    public Task RestoreOriginalAsync(string versionPath, string storedName, Func<Stream, Task> write, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Stream? OpenOriginal(string versionPath, string storedName) => throw new NotSupportedException();

    public string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId) =>
        throw new NotSupportedException();

    public string GetInstructionsFolder(string courseName, string assignmentTitle, string assignmentId) =>
        throw new NotSupportedException();

    public ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber) => throw new NotSupportedException();

    public Task<CodeExtraction> ReExtractAsync(string versionPath, IReadOnlyList<ExtractableFile> files, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
