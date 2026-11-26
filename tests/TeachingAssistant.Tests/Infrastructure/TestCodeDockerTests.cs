using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Testing;
using TeachingAssistant.Infrastructure.Sandbox;
using TeachingAssistant.Tests.Application;

namespace TeachingAssistant.Tests.Infrastructure;

/// <summary>Generated test code against the real Docker sandbox and its JUnit jar; skipped without Docker or the ta-java:27 image.</summary>
[Collection(DockerCollection.Name)]
public class TestCodeDockerTests
{
    private const string Interface = "public interface Account {\n    void deposit(int amount);\n    int balance();\n}\n";

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
        }
        """;

    private const string Stub =
        """
        class StudentAdapter implements Account {
            public void deposit(int amount) { throw new UnsupportedOperationException(); }
            public int balance() { throw new UnsupportedOperationException(); }
        }
        """;

    private readonly DockerSandboxRunner _sandbox = new(Options.Create(new SandboxOptions()), NullLogger<DockerSandboxRunner>.Instance);

    [Fact]
    public async Task GeneratedTests_CompileWithTheInterfaceAndThePlaceholder_AgainstTheImagesJUnit()
    {
        SkipWithoutDocker();
        var testSets = new FakeTestSets();
        var testSet = ApprovedTestSet();
        testSets.Add(testSet);
        var service = new TestCodeService(testSets, new ScriptedModel(Tests, Stub), new ModelCallContext(), _sandbox, TimeProvider.System);

        await service.GenerateAsync(testSet.Id, TestContext.Current.CancellationToken);

        var code = TestDesignService.ReadContent(testSet).Code!;
        Assert.Equal((TestCodeStatus.Compiled, null), (code.Status, code.CompileOutput));
    }

    [Fact]
    public async Task TheCompiledTests_RunWithTheJUnitConsole_AndReportEachTestByName()
    {
        SkipWithoutDocker();
        const string adapter =
            """
            class StudentAdapter implements Account {
                private final Wallet wallet = new Wallet();
                public void deposit(int amount) { wallet.addMoney(amount); }
                public int balance() { return wallet.getMoney(); }
            }
            """;
        const string student = "class Wallet {\n    private int money;\n    void addMoney(int m) { money = m; }\n    int getMoney() { return money; }\n}\n";

        var result = await _sandbox.RunAsync(new SandboxRequest
        {
            Image = JavaBuildCheck.Image,
            Files =
            [
                File("Account.java", Interface),
                File("Wallet.java", student),
                File(TestCodeService.AdapterFile, adapter),
                File(TestCodeService.TestClassFile, Tests),
            ],
            Steps =
            [
                new SandboxStep
                {
                    Name = "compile",
                    Command = ["javac", "-d", "out", "-cp", TestCodeService.JUnitJar, "Account.java", "Wallet.java",
                        TestCodeService.AdapterFile, TestCodeService.TestClassFile],
                    StopOnFailure = true,
                },
                new SandboxStep
                {
                    Name = "test",
                    Command = ["java", "-jar", TestCodeService.JUnitJar, "execute", "--class-path", "out", "--select-class",
                        "AssignmentTests", "--details", "tree", "--disable-banner"],
                },
            ],
        }, TestContext.Current.CancellationToken);

        Assert.Equal(SandboxStepOutcome.Succeeded, result.Steps[0].Outcome);
        var test = result.Steps[1];
        Assert.Equal(SandboxStepOutcome.Failed, test.Outcome);
        Assert.Contains("T1 Deposit", test.Stdout);
        Assert.Contains("T2 Two deposits", test.Stdout);
        Assert.Contains("balance after deposit(1), deposit(2)", test.Stdout);
        Assert.Contains("1 tests successful", test.Stdout);
        Assert.Contains("1 tests failed", test.Stdout);
    }

    private static SandboxFile File(string path, string text) => new() { Path = path, Content = Encoding.UTF8.GetBytes(text) };

    private static TestSet ApprovedTestSet()
    {
        var content = new TestSetContent
        {
            Requirements = [new TestRequirement { Id = "R1", Text = "Deposit increases the balance", Testable = true }],
            Interface = Interface,
            Cases = [new TestCase { Id = "T1", Title = "Deposit", Category = TestCategory.Normal, RequirementIds = ["R1"],
                Steps = "deposit(100)\nbalance()", Expected = "100" }],
        };
        var request = new GenerateTestsRequest
        {
            Style = TestStyle.ClassDesign,
            Categories = [TestCategory.Normal],
            Amount = TestAmount.AboutThreePerCategory,
            ReferenceFiles = [],
        };
        var testSet = new TestSet("course", "assignment", 1, TestStyle.ClassDesign,
            JsonSerializer.Serialize(request, TestDesignService.JsonOptions), JsonSerializer.Serialize(content, TestDesignService.JsonOptions),
            "model", DateTimeOffset.UtcNow);
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

    private sealed class ScriptedModel(string testClass, string stub) : ILanguageModel
    {
        public string ModelId => "model";

        public string GradingModelId => "model";


        public string AdapterModelId => "adapter-model";
        public Task<LanguageModelReply> WriteAdapterAsync(

            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>

            throw new NotSupportedException();


        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LanguageModelReply
            {
                Model = "model",
                Text = JsonSerializer.Serialize(new { testClass, stubAdapter = stub }),
            });

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
            IReadOnlyList<TextPart> system, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
