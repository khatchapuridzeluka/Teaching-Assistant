using System.Text;
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
        Assert.Equal(["compile task1/", "compile task2/", "checkstyle", "pmd"], request.Steps.Select(s => s.Name));
        Assert.Equal(["pmd", "check", "--no-progress", "--no-cache", "-d", ".", "-R", "__ta_pmd.xml", "-f", "text",
            "--aux-classpath", "/tmp/classes-1:/tmp/classes-2"], request.Steps[3].Command);
        Assert.Contains("category/java/errorprone.xml/CloseResource", Text(request, "__ta_pmd.xml"));
        Assert.Equal(["checkstyle", "-c", "__ta_checkstyle.xml", "."], request.Steps[2].Command);
        Assert.Contains("<module name=" + '"' + "TypeName" + '"' + "/>", Text(request, "__ta_checkstyle.xml"));
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
        _sandbox.Result = Steps(new SandboxStepResult
        {
            Name = "compile",
            Outcome = SandboxStepOutcome.Failed,
            ExitCode = 1,
            Stdout = "",
            Stderr = "Main.java:1: error: incompatible types: String cannot be converted to int\n1 error\n",
            OutputTruncated = false,
            Duration = TimeSpan.FromSeconds(1),
        });

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
        _sandbox.Result = Steps(new SandboxStepResult
        {
            Name = "compile",
            Outcome = SandboxStepOutcome.Succeeded,
            ExitCode = 0,
            Stdout = "",
            Stderr = "Main.java:3: warning: [unchecked] unchecked call\nMain.java:5: warning: [deprecation] old\n2 warnings\n",
            OutputTruncated = false,
            Duration = TimeSpan.Zero,
        });

        var compiled = await Check().CheckAsync(Version(), TestContext.Current.CancellationToken);
        Assert.Equal((BuildStatus.Compiled, 2), (compiled.Status, compiled.Warnings));
        Assert.Contains("Result: it compiled without errors, with 2 compiler warnings.", JavaBuildCheck.PromptSection(compiled));

        _sandbox.Result = Steps(new SandboxStepResult
        {
            Name = "compile",
            Outcome = SandboxStepOutcome.TimedOut,
            Stdout = "",
            Stderr = "",
            OutputTruncated = false,
            Duration = TimeSpan.FromSeconds(30),
        });
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

    [Fact]
    public void ReadCheckstyle_KeepsTheFindingsWithTheSubmissionsPaths_AndCountsUnreadableFiles()
    {
        var step = new SandboxStepResult
        {
            Name = "checkstyle",
            Outcome = SandboxStepOutcome.Failed,
            ExitCode = 1,
            Stdout = "Starting audit...\n[WARN] /work/./src/Zoo.java:3:16: Name 'Count' must match pattern. [MemberName]\n[WARN] /work/./src/Zoo.java:9:9: 'if' construct must use '{}'s. [NeedBraces]\n[ERROR] /work/./broken/Broken.java:1: Got an exception - parse error\nAudit done.\n",
            Stderr = "",
            OutputTruncated = false,
            Duration = TimeSpan.Zero,
        };

        var (findings, output) = JavaBuildCheck.ReadCheckstyle(step);

        Assert.Equal(2, findings);
        Assert.Equal("src/Zoo.java:3:16: Name 'Count' must match pattern. [MemberName]\nsrc/Zoo.java:9:9: 'if' construct must use '{}'s. [NeedBraces]\n(1 file could not be read by Checkstyle, usually because of a syntax error)", output);
    }

    [Fact]
    public void ReadCheckstyle_ThatDidNotFinish_HasNoCount()
    {
        Assert.Null(JavaBuildCheck.ReadCheckstyle(null).Findings);
        Assert.Equal((null, "it took too long and was stopped"), JavaBuildCheck.ReadCheckstyle(
            new SandboxStepResult
            {
                Name = "checkstyle",
                Outcome = SandboxStepOutcome.TimedOut,
                Stdout = "",
                Stderr = "",
                OutputTruncated = false,
                Duration = TimeSpan.Zero,
            }));
        Assert.Null(JavaBuildCheck.ReadCheckstyle(
            new SandboxStepResult
            {
                Name = "checkstyle",
                Outcome = SandboxStepOutcome.Failed,
                ExitCode = 254,
                Stdout = "",
                Stderr = "Exception in thread main",
                OutputTruncated = false,
                Duration = TimeSpan.Zero,
            }).Findings);
    }

    [Fact]
    public void ReadPmd_KeepsTheFindingsWithTheSubmissionsPaths_AndCountsUnparsableFiles()
    {
        var step = new SandboxStepResult
        {
            Name = "pmd",
            Outcome = SandboxStepOutcome.Failed,
            ExitCode = 5,
            Stdout = "./src/Main.java:10:\tCloseResource:\tEnsure that resources like this Scanner object are closed after use\n./src/Main.java:13:\tCompareObjectsWithEquals:\tUse equals() to compare object references.\n./broken/Broken.java\t-\tParseException: Parse exception in file\nWas expecting:\n    \")\" ...\n",
            Stderr = "[INFO] An error occurred while executing PMD.",
            OutputTruncated = false,
            Duration = TimeSpan.Zero,
        };

        var (findings, output) = JavaBuildCheck.ReadPmd(step);

        Assert.Equal(2, findings);
        Assert.Equal("src/Main.java:10: Ensure that resources like this Scanner object are closed after use [CloseResource]\nsrc/Main.java:13: Use equals() to compare object references. [CompareObjectsWithEquals]\n(1 file could not be read by PMD, usually because of a syntax error)", output);
    }

    [Fact]
    public void ReadPmd_WithNothingFound_OrAnUnexpectedExit()
    {
        Assert.Equal((0, ""), JavaBuildCheck.ReadPmd(new SandboxStepResult
        {
            Name = "pmd",
            Outcome = SandboxStepOutcome.Succeeded,
            ExitCode = 0,
            Stdout = "",
            Stderr = "",
            OutputTruncated = false,
            Duration = TimeSpan.Zero,
        }));
        Assert.Equal((null, "[ERROR] Cannot load ruleset __ta_pmd.xml"), JavaBuildCheck.ReadPmd(new SandboxStepResult
        {
            Name = "pmd",
            Outcome = SandboxStepOutcome.Failed,
            ExitCode = 1,
            Stdout = "",
            Stderr = "[WARN] x\n[ERROR] Cannot load ruleset __ta_pmd.xml\n",
            OutputTruncated = false,
            Duration = TimeSpan.Zero,
        }));
    }

    [Fact]
    public void ThePromptSection_ListsTheCheckstyleFindings_AfterTheCompileResult()
    {
        var section = JavaBuildCheck.PromptSection(new BuildCheckOutcome
        {
            Status = BuildStatus.Compiled,
            Output = "",
            Programs = 1,
            Warnings = 0,
            StyleFindings = 1,
            StyleOutput = "src/Zoo.java:3:16: Name 'Count' must match pattern. [MemberName]",
        })!;

        Assert.Contains("Result: it compiled without errors.\nCheckstyle (a style checker with the teacher's short list: naming conventions,",
            section);
        Assert.Contains("found 1 issue (file:line:column: message [rule]):\nsrc/Zoo.java:3:16: Name 'Count'", section);
        Assert.Contains("found nothing.", JavaBuildCheck.PromptSection(new BuildCheckOutcome
        {
            Status = BuildStatus.Compiled,
            Output = "",
            Programs = 1,
            Warnings = 0,
            StyleFindings = 0,
            StyleOutput = "",
        }));
        Assert.Contains("PMD (a code checker with the teacher's short list: resources such as a Scanner", section);
        Assert.Contains("PMD (a code checker", section[section.IndexOf("Checkstyle", StringComparison.Ordinal)..]);
        Assert.Contains("found 1 issue (file:line: message [rule]):\nsrc/A.java:2: Use equals()", JavaBuildCheck.PromptSection(
            new BuildCheckOutcome
            {
                Status = BuildStatus.Compiled,
                Output = "",
                Programs = 1,
                Warnings = 0,
                StyleFindings = 0,
                StyleOutput = "",
                PmdFindings = 1,
                PmdOutput = "src/A.java:2: Use equals() [CompareObjectsWithEquals]",
            }));
    }

    private static SandboxResult Steps(params SandboxStepResult[] steps) => new()
    {
        Steps = steps,
        Duration = TimeSpan.FromSeconds(2),
    };

    private static SandboxStepResult Succeeded(string name) =>
        new()
        {
            Name = name,
            Outcome = SandboxStepOutcome.Succeeded,
            ExitCode = 0,
            Stdout = "",
            Stderr = "",
            OutputTruncated = false,
            Duration = TimeSpan.FromSeconds(1),
        };

    private static string Text(SandboxRequest request, string path) =>
        Encoding.UTF8.GetString(request.Files.Single(f => f.Path == path).Content);
}
