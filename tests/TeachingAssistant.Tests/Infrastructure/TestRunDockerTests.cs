using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Testing;
using TeachingAssistant.Infrastructure.Sandbox;
using TeachingAssistant.Tests.Application;

namespace TeachingAssistant.Tests.Infrastructure;

/// <summary>A whole test run against the real Docker sandbox and its JUnit jar; skipped without Docker or the ta-java:27 image.</summary>
[Collection(DockerCollection.Name)]
public class TestRunDockerTests
{
    private const string Interface = "public interface Account {\n    void deposit(int amount);\n    int balance();\n    void reset();\n}\n";

    private const string Tests =
        """
        import org.junit.jupiter.api.*;
        import static org.junit.jupiter.api.Assertions.*;

        @Timeout(5)
        class AssignmentTests {
            @Test
            @DisplayName("T1 Deposit")
            void t1_deposit() {
                Account a = new StudentAdapter();
                a.deposit(100);
                assertEquals(100, a.balance(), "balance after deposit(100)");
            }

            @Test
            @DisplayName("T2 Two deposits")
            void t2_twoDeposits() {
                Account a = new StudentAdapter();
                a.deposit(1);
                a.deposit(2);
                assertEquals(3, a.balance(), "balance after deposit(1), deposit(2)");
            }

            @Test
            @DisplayName("T3 Reset")
            void t3_reset() {
                Account a = new StudentAdapter();
                a.deposit(5);
                a.reset();
                assertEquals(0, a.balance(), "balance after reset()");
            }
        }
        """;

    private const string Adapter =
        """
        import shop.Wallet;

        class StudentAdapter implements Account {
            private final Wallet wallet = new Wallet();
            public void deposit(int amount) { wallet.addMoney(amount); }
            public int balance() { return wallet.getMoney(); }
            public void reset() { throw new UnsupportedOperationException("not implemented: reset"); }
        }
        """;

    [Fact]
    public async Task ABuggyStudent_PassesOneCase_FailsOneWithTheAssertionMessage_AndMissesOneMethod()
    {
        SkipWithoutDocker();
        var testSets = new FakeTestSets();
        var testSet = ApprovedTestSet();
        testSets.Add(testSet);
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", "student-1", "Student", "sub-1", "folder",
            DateTimeOffset.UtcNow);
        var version = submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
        var storage = new FakeCodeStorage();
        storage.Files["src/shop/Wallet.java"] = Encoding.UTF8.GetBytes(
            "package shop;\npublic class Wallet {\n    private int money;\n    public void addMoney(int m) { money = m; }\n    public int getMoney() { return money; }\n}\n");
        var runs = new Runs();
        var sandbox = new DockerSandboxRunner(Options.Create(new SandboxOptions()), NullLogger<DockerSandboxRunner>.Instance);
        var service = new TestRunService(testSets, runs, new Submissions(submission), new JavaBuildCheck(sandbox, storage), sandbox,
            new AdapterModel(), new ModelCallContext(), new FakeTestCodeJobs(), TimeProvider.System);

        await service.RunAsync(testSet.Id, version.Id, TestContext.Current.CancellationToken);

        var run = Assert.Single(runs.All);
        Assert.True(run.Status == TestRunStatus.Completed, run.Output);
        var results = JsonSerializer.Deserialize<TestRunDetails>(run.Results, TestDesignService.JsonOptions)!.Results;
        Assert.Equal([TestOutcome.Passed, TestOutcome.Failed, TestOutcome.NotImplemented], results.Select(r => r.Outcome));
        Assert.StartsWith("balance after deposit(1), deposit(2) ==> expected: <3> but was: <2>", results[1].Message);
        Assert.Equal("not implemented: reset", results[2].Message);
    }

    private static TestSet ApprovedTestSet()
    {
        TestCase Case(string id, string title) => new()
        {
            Id = id, Title = title, Category = TestCategory.Normal, RequirementIds = [], Steps = "x", Expected = "y",
        };
        var content = new TestSetContent
        {
            Requirements = [],
            Interface = Interface,
            Cases = [Case("T1", "Deposit"), Case("T2", "Two deposits"), Case("T3", "Reset")],
            Code = new TestCode
            {
                InterfaceFile = "Account.java",
                TestClass = Tests,
                StubAdapter = "class StudentAdapter { }",
                Status = TestCodeStatus.Compiled,
                Model = "model",
                CreatedAt = DateTimeOffset.UtcNow,
            },
        };
        var request = new GenerateTestsRequest { Style = TestStyle.ClassDesign, Categories = [TestCategory.Normal],
            Amount = TestAmount.AboutThreePerCategory, ReferenceFiles = [] };
        var testSet = new TestSet("course", "assignment", 1, TestStyle.ClassDesign, JsonSerializer.Serialize(request, TestDesignService.JsonOptions),
            JsonSerializer.Serialize(content, TestDesignService.JsonOptions), "model", DateTimeOffset.UtcNow);
        testSet.Approve();
        return testSet;
    }

    private static void SkipWithoutDocker()
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "image", "inspect", "--format", "{{.Id}}", JavaBuildCheck.Image })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.SkipWhen(process.ExitCode != 0, $"The {JavaBuildCheck.Image} image is not built.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Skip("Docker is not installed.");
        }
    }

    private sealed class Runs : ITestRunStore
    {
        public List<TestRun> All { get; } = [];

        public Task<IReadOnlyList<TestRun>> GetLatestByTestSetAsync(Guid testSetId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TestRun>>(All);

        public Task<TestRun?> FindLatestWithAdapterAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TestRun?>(null);

        public void Add(TestRun run) => All.Add(run);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Submissions(Submission submission) : ISubmissionStore
    {
        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
            SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>([submission]);

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(
            SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Submission?>(submission);

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission added) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AdapterModel : ILanguageModel
    {
        public string ModelId => "model";

        public string GradingModelId => "model";

        public string AdapterModelId => "adapter-model";

        public Task<LanguageModelReply> WriteAdapterAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LanguageModelReply
            {
                Model = "adapter-model",
                Text = JsonSerializer.Serialize(new { adapter = Adapter, notImplemented = new[] { "reset" }, notes = "" }),
            });

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
