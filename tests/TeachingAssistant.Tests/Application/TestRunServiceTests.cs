using System.Text;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

public class TestRunServiceTests
{
    private const string Interface = "public interface Account {\n    void deposit(int amount);\n    int balance();\n}";

    private const string Report =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <testsuite name="JUnit Jupiter" tests="3">
          <testcase name="t1_deposit()" classname="AssignmentTests" time="0.01"/>
          <testcase name="t2_twoDeposits()" classname="AssignmentTests" time="0.01">
            <failure message="balance after deposit(1), deposit(2) ==&gt; expected: &lt;3&gt; but was: &lt;2&gt;" type="org.opentest4j.AssertionFailedError">stack</failure>
          </testcase>
          <testcase name="t3_transfer()" classname="AssignmentTests" time="0.01">
            <error message="not implemented: transferTo" type="java.lang.UnsupportedOperationException">stack</error>
          </testcase>
        </testsuite>
        """;

    private readonly FakeTestSets _testSets = new();
    private readonly FakeRuns _runs = new();
    private readonly FakeSubmissions _submissions = new();
    private readonly FakeCodeStorage _storage = new();
    private readonly ScriptedSandbox _sandbox = new();
    private readonly AdapterModel _model = new();
    private readonly FakeTestCodeJobs _jobs = new();

    [Fact]
    public async Task Run_CompilesTheStudent_WritesTheAdapter_RunsTheTests_AndStoresOneResultPerCase()
    {
        var (testSet, version) = Setup();
        _sandbox.Script.Enqueue(Ok("compile submission"));
        _sandbox.Script.Enqueue(TestRun(Report));

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        Assert.Equal(PromptLibrary.TestAdapter, _model.SystemPrompt);
        var prompt = Assert.Single(_model.Prompts);
        Assert.Contains("<interface file=\"Account.java\">\npublic interface Account {", prompt);
        Assert.Contains("<file path=\"src/Wallet.java\">\nclass Wallet { }", prompt);
        Assert.Contains("R1: Deposit increases the balance", prompt);
        Assert.DoesNotContain("Ana Beridze", prompt);
        Assert.DoesNotContain("expected", prompt);

        var full = _sandbox.Requests[1];
        Assert.Equal(["src/Wallet.java", "Account.java", "StudentAdapter.java", "AssignmentTests.java", "__ta_all_sources.txt"],
            full.Files.Select(f => f.Path));
        Assert.Equal(["compile", "test", "report"], full.Steps.Select(s => s.Name));

        var run = Assert.Single(_runs.All);
        Assert.Equal((TestRunStatus.Completed, 1, "adapter-model"), (run.Status, run.TestSetVersion, run.AdapterModel));
        Assert.Contains("class StudentAdapter", run.Adapter);
        var response = Assert.Single((await Service().GetLatestAsync("assignment", testSet.Id, TestContext.Current.CancellationToken))!);
        Assert.Equal(("Ana Beridze", 1, 1, 1, 1), (response.StudentName, response.VersionNumber, response.Passed, response.Failed, response.NotImplemented));
        Assert.Equal([TestOutcome.Passed, TestOutcome.Failed, TestOutcome.NotImplemented], response.Results.Select(r => r.Outcome));
        Assert.Equal("balance after deposit(1), deposit(2) ==> expected: <3> but was: <2>", response.Results[1].Message);
        Assert.Equal(["transferTo"], response.Unmapped);
    }

    [Fact]
    public async Task Run_WhenTheStudentsCodeDoesNotCompile_StopsBeforeAnyModelCall()
    {
        var (testSet, version) = Setup();
        _sandbox.Script.Enqueue(Failed("compile submission", "src/Wallet.java:1: error: ';' expected"));

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        Assert.Empty(_model.Prompts);
        var run = Assert.Single(_runs.All);
        Assert.Equal(TestRunStatus.StudentCompileFailed, run.Status);
        Assert.Contains("';' expected", run.Output);
    }

    [Fact]
    public async Task Run_WhenTheAdapterDoesNotCompile_RepairsItOnceWithTheCompilerErrors()
    {
        var (testSet, version) = Setup();
        _sandbox.Script.Enqueue(Ok("compile submission"));
        _sandbox.Script.Enqueue(Failed("compile", "StudentAdapter.java:3: error: cannot find symbol addMoney"));
        _sandbox.Script.Enqueue(TestRun(Report));

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, _model.Prompts.Count);
        Assert.Contains("<compiler_errors>\nStudentAdapter.java:3: error: cannot find symbol addMoney", _model.Prompts[1]);
        Assert.Contains("<previous_adapter>", _model.Prompts[1]);
        Assert.Equal(TestRunStatus.Completed, Assert.Single(_runs.All).Status);
    }

    [Fact]
    public async Task Run_WhenTheRepairedAdapterStillDoesNotCompile_IsAHarnessProblemNotTheStudents()
    {
        var (testSet, version) = Setup();
        _sandbox.Script.Enqueue(Ok("compile submission"));
        _sandbox.Script.Enqueue(Failed("compile", "StudentAdapter.java:3: error: one"));
        _sandbox.Script.Enqueue(Failed("compile", "StudentAdapter.java:3: error: two"));

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        var run = Assert.Single(_runs.All);
        Assert.Equal(TestRunStatus.HarnessFailed, run.Status);
        Assert.Contains("no test result counts against the student", run.Output);
        Assert.Contains("error: two", run.Output);
    }

    [Fact]
    public async Task Run_Again_ReusesTheStoredAdapter_WithoutAModelCall()
    {
        var (testSet, version) = Setup();
        _sandbox.Script.Enqueue(Ok("compile submission"));
        _sandbox.Script.Enqueue(TestRun(Report));
        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);
        _sandbox.Script.Enqueue(Ok("compile submission"));
        _sandbox.Script.Enqueue(TestRun(Report));

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        Assert.Single(_model.Prompts);
        Assert.Equal(2, _runs.All.Count);
        Assert.Equal(_runs.All[0].Adapter, _runs.All[1].Adapter);
        Assert.Equal(["transferTo"], (await Service().GetLatestAsync("assignment", testSet.Id, TestContext.Current.CancellationToken))![0].Unmapped);
    }

    [Fact]
    public async Task Run_WithoutDocker_RecordsThatItDidNotRun()
    {
        var (testSet, version) = Setup();
        _sandbox.Unavailable = true;

        await Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        var run = Assert.Single(_runs.All);
        Assert.Equal((TestRunStatus.NotRun, "Docker is not running."), (run.Status, run.Output));
    }

    [Fact]
    public async Task Run_WithoutTestCode_IsRefused()
    {
        var (testSet, version) = Setup(withCode: false);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken));
        Assert.Empty(_sandbox.Requests);
    }

    [Fact]
    public async Task QueueAll_QueuesEveryStudentsLatestVersion_AndSkipsStudentsWithoutWork()
    {
        var (testSet, version) = Setup();
        var other = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", "student-2", "Nino Gelashvili", "sub-2", "folder",
            DateTimeOffset.UtcNow);
        _submissions.All.Add(other);

        var queued = (await Service().QueueAllAsync("assignment", testSet.Id, TestContext.Current.CancellationToken))!;

        Assert.Equal(1, queued.Queued);
        Assert.Equal(["Nino Gelashvili: no stored work to test"], queued.Skipped);
        Assert.Equal([version.Id], _jobs.RunJobs);
    }

    [Fact]
    public async Task QueueOne_QueuesThatVersion_AndFindsNothingForAVersionOfAnotherAssignment()
    {
        var (testSet, version) = Setup();
        var elsewhere = new Submission(SubmissionSource.GoogleClassroom, "course", "other-assignment", "student-3", "Other", "sub-3", "folder",
            DateTimeOffset.UtcNow);
        var otherVersion = elsewhere.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
        _submissions.All.Add(elsewhere);

        var job = await Service().QueueOneAsync("assignment", testSet.Id, version.Id, TestContext.Current.CancellationToken);
        var none = await Service().QueueOneAsync("assignment", testSet.Id, otherVersion.Id, TestContext.Current.CancellationToken);

        Assert.Equal(version.Id, job!.VersionId);
        Assert.Null(none);
        Assert.Equal([version.Id], _jobs.RunJobs);
    }

    [Fact]
    public void ReadReport_MapsTimeoutsAndExceptions_AndMarksCasesThatDidNotRun()
    {
        const string xml =
            """
            <testsuite>
              <testcase name="t1_loop()"><error message="t1_loop() timed out after 5 seconds" type="java.util.concurrent.TimeoutException"/></testcase>
              <testcase name="t2_npe()"><error message="Cannot invoke &quot;Node.next&quot;" type="java.lang.NullPointerException"/></testcase>
            </testsuite>
            """;
        TestCase Case(string id) => new() { Id = id, Title = id, Category = TestCategory.Normal, RequirementIds = [], Steps = "x" };

        var results = TestRunService.ReadReport(xml, [Case("T1"), Case("T2"), Case("T3")]);

        Assert.Equal([TestOutcome.Failed, TestOutcome.Failed, TestOutcome.Missing], results.Select(r => r.Outcome));
        Assert.Contains("did not finish within 5 seconds", results[0].Message);
        Assert.Equal("NullPointerException: Cannot invoke \"Node.next\"", results[1].Message);
    }

    private (TestSet TestSet, SubmissionVersion Version) Setup(bool withCode = true)
    {
        var content = new TestSetContent
        {
            Requirements = [new TestRequirement { Id = "R1", Text = "Deposit increases the balance", Testable = true }],
            Interface = Interface,
            Cases =
            [
                Case("T1", "Deposit", "deposit(100)\nbalance()", "100"),
                Case("T2", "Two deposits", "deposit(1)\ndeposit(2)\nbalance()", "3"),
                Case("T3", "Transfer", "a.transferTo(b, 1)", "1"),
            ],
            Code = withCode
                ? new TestCode
                {
                    InterfaceFile = "Account.java",
                    TestClass = "class AssignmentTests { }",
                    StubAdapter = "class StudentAdapter { }",
                    Status = TestCodeStatus.Compiled,
                    Model = "model",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
                : null,
        };
        var request = new GenerateTestsRequest { Style = TestStyle.ClassDesign, Categories = [TestCategory.Normal],
            Amount = TestAmount.AboutThreePerCategory, ReferenceFiles = [] };
        var testSet = new TestSet("course", "assignment", 1, TestStyle.ClassDesign, JsonSerializer.Serialize(request, TestDesignService.JsonOptions),
            JsonSerializer.Serialize(content, TestDesignService.JsonOptions), "model", DateTimeOffset.UtcNow);
        testSet.Approve();
        _testSets.Add(testSet);

        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", "student-1", "Ana Beridze", "sub-1", "folder",
            DateTimeOffset.UtcNow);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
        _submissions.All.Add(submission);
        _storage.Files["src/Wallet.java"] = Encoding.UTF8.GetBytes("class Wallet { }\n");
        return (testSet, version);
    }

    private static TestCase Case(string id, string title, string steps, string expected) => new()
    {
        Id = id, Title = title, Category = TestCategory.Normal, RequirementIds = ["R1"], Steps = steps, Expected = expected,
    };

    private static SandboxResult Ok(string name) => Result((name, SandboxStepOutcome.Succeeded, ""));

    private static SandboxResult Failed(string name, string stderr) => Result((name, SandboxStepOutcome.Failed, stderr));

    private static SandboxResult TestRun(string report) => Result(
        ("compile", SandboxStepOutcome.Succeeded, ""), ("test", SandboxStepOutcome.Failed, ""), ("report", SandboxStepOutcome.Succeeded, report));

    private static SandboxResult Result(params (string Name, SandboxStepOutcome Outcome, string Text)[] steps) => new()
    {
        Steps = steps.Select(s => new SandboxStepResult
        {
            Name = s.Name,
            Outcome = s.Outcome,
            Stdout = s.Name == "report" ? s.Text : "",
            Stderr = s.Name == "report" ? "" : s.Text,
            OutputTruncated = false,
            Duration = TimeSpan.FromSeconds(1),
        }).ToList(),
        Duration = TimeSpan.FromSeconds(1),
    };

    private TestRunService Service() => new(_testSets, _runs, _submissions, new JavaBuildCheck(_sandbox, _storage), _sandbox, _model,
        new ModelCallContext(), _jobs, TimeProvider.System);

    private sealed class ScriptedSandbox : ISandboxRunner
    {
        public Queue<SandboxResult> Script { get; } = new();

        public List<SandboxRequest> Requests { get; } = [];

        public bool Unavailable { get; set; }

        public Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Unavailable)
            {
                throw new SandboxUnavailableException("Docker is not running.");
            }

            return Task.FromResult(Script.Dequeue());
        }
    }

    private sealed class FakeRuns : ITestRunStore
    {
        public List<TestRun> All { get; } = [];

        public Task<IReadOnlyList<TestRun>> GetLatestByTestSetAsync(Guid testSetId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TestRun>>(All.Where(r => r.TestSetId == testSetId).AsEnumerable().Reverse().DistinctBy(r => r.VersionId).ToList());

        public Task<TestRun?> FindLatestWithAdapterAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.LastOrDefault(r => r.VersionId == versionId && r.Status == TestRunStatus.Completed && r.Adapter != null));

        public void Add(TestRun run) => All.Add(run);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmissions : ISubmissionStore
    {
        public List<Submission> All { get; } = [];

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
            SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(All.Where(s => s.AssignmentId == assignmentId).ToList());

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(
            SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.FirstOrDefault(s => s.Versions.Any(v => v.Id == versionId)));

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => All.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AdapterModel : ILanguageModel
    {
        public string? SystemPrompt { get; private set; }

        public List<string> Prompts { get; } = [];

        public string ModelId => "model";

        public string GradingModelId => "model";

        public string AdapterModelId => "adapter-model";

        public Task<LanguageModelReply> WriteAdapterAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            Prompts.Add(string.Join("\n", parts.OfType<TextPart>().Select(p => p.Text)));
            var reply = JsonSerializer.Serialize(new
            {
                adapter = "class StudentAdapter implements Account { }",
                notImplemented = new[] { "transferTo" },
                notes = "",
            });
            return Task.FromResult(new LanguageModelReply { Model = "adapter-model", Text = reply });
        }

        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
