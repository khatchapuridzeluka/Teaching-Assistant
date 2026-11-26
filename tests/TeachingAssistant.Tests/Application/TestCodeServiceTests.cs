using System.Text;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

public class TestCodeServiceTests
{
    private const string Interface = "public interface Account {\n    void deposit(int amount);\n    int balance();\n}";

    private const string Answer =
        """
        {
          "testClass": "import org.junit.jupiter.api.*;\nclass AssignmentTests {\n  @Test void t1() { Account a = new StudentAdapter(); }\n}",
          "stubAdapter": "class StudentAdapter implements Account {\n  public void deposit(int amount) { throw new UnsupportedOperationException(); }\n  public int balance() { throw new UnsupportedOperationException(); }\n}"
        }
        """;

    private readonly FakeTestSets _testSets = new();
    private readonly FakeModel _model = new();
    private readonly FakeSandbox _sandbox = new();

    [Fact]
    public async Task Generate_SendsTheInterfaceAndCases_CompilesInTheSandbox_AndStoresTheCodeWithTheApprovedVersion()
    {
        var testSet = Approved();

        await Service().GenerateAsync(testSet.Id, TestContext.Current.CancellationToken);

        Assert.Equal(PromptLibrary.TestCode, _model.SystemPrompt);
        Assert.Contains("<interface file=\"Account.java\">\npublic interface Account {", _model.Prompt);
        Assert.Contains("<case id=\"T1\" title=\"Deposit\">\nChecks: R1 Deposit increases the balance\nSteps:\ndeposit(100)\nbalance()\nExpected:\n100",
            _model.Prompt);
        Assert.DoesNotContain("<reference_solution>", _model.Prompt);

        var run = Assert.Single(_sandbox.Requests);
        Assert.Equal(["Account.java", "StudentAdapter.java", "AssignmentTests.java"], run.Files.Select(f => f.Path));
        Assert.Equal(["javac", "-encoding", "UTF-8", "-d", "out", "-cp", "/opt/junit/junit.jar", "Account.java", "StudentAdapter.java",
            "AssignmentTests.java"], Assert.Single(run.Steps).Command);
        Assert.StartsWith("public interface Account", Encoding.UTF8.GetString(run.Files[0].Content));

        var code = Code(testSet);
        Assert.Equal(("Account.java", TestCodeStatus.Compiled, null, "model"), (code.InterfaceFile, code.Status, code.CompileOutput, code.Model));
        Assert.Contains("class AssignmentTests", code.TestClass);
        Assert.Contains("class StudentAdapter", code.StubAdapter);
        Assert.Equal("Deposit", TestDesignService.ReadContent(testSet).Cases[0].Title);
    }

    [Fact]
    public async Task Generate_WhenTheTestsDoNotCompile_KeepsTheCodeWithTheCompilerErrors()
    {
        var testSet = Approved();
        _sandbox.Result = Step(SandboxStepOutcome.Failed, "AssignmentTests.java:3: error: cannot find symbol");

        await Service().GenerateAsync(testSet.Id, TestContext.Current.CancellationToken);

        var code = Code(testSet);
        Assert.Equal(TestCodeStatus.CompileFailed, code.Status);
        Assert.Contains("cannot find symbol", code.CompileOutput);
    }

    [Fact]
    public async Task Generate_WhenTheImageHasNoJUnitOrTheSandboxIsDown_MarksTheCodeNotChecked()
    {
        var testSet = Approved();
        _sandbox.Result = Step(SandboxStepOutcome.Failed, "AssignmentTests.java:1: error: package org.junit.jupiter.api does not exist");

        await Service().GenerateAsync(testSet.Id, TestContext.Current.CancellationToken);
        Assert.Equal(TestCodeStatus.NotChecked, Code(testSet).Status);
        Assert.Contains("Rebuild it", Code(testSet).CompileOutput);

        _sandbox.Unavailable = true;
        await Service().GenerateAsync(testSet.Id, TestContext.Current.CancellationToken);
        Assert.Equal((TestCodeStatus.NotChecked, "Docker is not running."), (Code(testSet).Status, Code(testSet).CompileOutput));
    }

    [Fact]
    public async Task Generate_ForADraftOrAConsoleTestSet_IsRefusedBeforeAnyModelCall()
    {
        var draft = Add(TestStyle.ClassDesign, approve: false);
        var console = Add(TestStyle.Console, approve: true);

        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().GenerateAsync(draft.Id, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().GenerateAsync(console.Id, TestContext.Current.CancellationToken));
        Assert.Null(_model.SystemPrompt);
    }

    [Fact]
    public async Task Generate_AnswerWithoutTheTestClass_FailsWithAnErrorThatIsNotRetried()
    {
        var testSet = Approved();
        _model.Reply = """{ "testClass": "class Other {}", "stubAdapter": "class StudentAdapter {}" }""";

        await Assert.ThrowsAsync<LanguageModelResponseException>(() =>
            Service().GenerateAsync(testSet.Id, TestContext.Current.CancellationToken));

        Assert.Null(TestDesignService.ReadContent(testSet).Code);
        Assert.Empty(_sandbox.Requests);
    }

    [Fact]
    public void InterfaceName_IsTheFirstInterfaceDeclared()
    {
        Assert.Equal("LinkedListHomework", TestCodeService.InterfaceName("public interface LinkedListHomework {\n  interface IntList {}\n}"));
        Assert.Null(TestCodeService.InterfaceName("class NotAnInterface {}"));
    }

    private static TestCode Code(TestSet testSet) => TestDesignService.ReadContent(testSet).Code!;

    private static SandboxResult Step(SandboxStepOutcome outcome, string stderr) => new()
    {
        Steps = [new SandboxStepResult { Name = "compile tests", Outcome = outcome, Stdout = "", Stderr = stderr, OutputTruncated = false,
            Duration = TimeSpan.FromSeconds(1) }],
        Duration = TimeSpan.FromSeconds(1),
    };

    private TestSet Approved() => Add(TestStyle.ClassDesign, approve: true);

    private TestSet Add(TestStyle style, bool approve)
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
            Style = style,
            Categories = [TestCategory.Normal],
            Amount = TestAmount.AboutThreePerCategory,
            ReferenceFiles = [],
        };
        var testSet = new TestSet("course", "assignment", _testSets.All.Count + 1, style,
            JsonSerializer.Serialize(request, TestDesignService.JsonOptions), JsonSerializer.Serialize(content, TestDesignService.JsonOptions),
            "model", DateTimeOffset.UtcNow);
        if (approve)
        {
            testSet.Approve();
        }

        _testSets.Add(testSet);
        return testSet;
    }

    private TestCodeService Service() => new(_testSets, _model, new ModelCallContext(), _sandbox, TimeProvider.System);

    private sealed class FakeSandbox : ISandboxRunner
    {
        public List<SandboxRequest> Requests { get; } = [];

        public SandboxResult Result { get; set; } = Step(SandboxStepOutcome.Succeeded, "");

        public bool Unavailable { get; set; }

        public Task<SandboxResult> RunAsync(SandboxRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Unavailable)
            {
                throw new SandboxUnavailableException("Docker is not running.");
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeModel : ILanguageModel
    {
        public string Reply { get; set; } = Answer;

        public string? SystemPrompt { get; private set; }

        public string Prompt { get; private set; } = "";

        public string ModelId => "model";

        public string GradingModelId => "model";


        public string AdapterModelId => "adapter-model";
        public Task<LanguageModelReply> WriteAdapterAsync(

            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>

            throw new NotSupportedException();


        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            Prompt = string.Join("\n", parts.OfType<TextPart>().Select(p => p.Text));
            return Task.FromResult(new LanguageModelReply { Model = "model", Text = Reply });
        }

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
