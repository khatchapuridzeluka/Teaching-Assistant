using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

public class TestDesignServiceTests
{
    private const string Answer =
        """
        {
          "requirements": [
            { "id": "REQ-deposit", "text": "Deposit increases the balance", "testable": true, "reason": "" },
            { "id": "REQ-overdraft", "text": "Withdrawing more than the balance is refused", "testable": true, "reason": "" },
            { "id": "REQ-style", "text": "Fields are private", "testable": false, "reason": "Only a reader can judge it." }
          ],
          "interface": "interface Account {\n    void deposit(int amount);\n}",
          "cases": [
            { "title": "Single deposit", "category": "Normal", "requirementIds": ["REQ-deposit"], "steps": "deposit(100)\nbalance()", "expected": "100" },
            { "title": "", "category": "Normal", "requirementIds": [], "steps": "x", "expected": "" },
            { "title": "Overdraft", "category": "Invalid", "requirementIds": ["REQ-overdraft", "REQ-unknown"], "steps": "deposit(70)\nwithdraw(100)", "expected": " " }
          ]
        }
        """;

    private readonly FakeModel _model = new();
    private readonly FakeTestSets _testSets = new();
    private readonly FakeGrading _grading = new();
    private readonly FakePolicyRules _rules = new();

    [Fact]
    public async Task Generate_SendsTheInstructionsSettingsPolicyAndReferenceSolution_AndSavesTheFirstDraft()
    {
        _rules.Add(1, "Typos", "A typo in a printed message is not a mistake.");

        var id = await Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken);

        Assert.Equal(PromptLibrary.Tests, _model.SystemPrompt);
        Assert.Equal("Bank account task", Assert.IsType<TextPart>(_model.Parts![0]).Text);
        var prompt = Assert.IsType<TextPart>(_model.Parts[^1]).Text;
        Assert.Contains("class design", prompt);
        Assert.Contains("normal cases, invalid input", prompt);
        Assert.Contains("Focus: Withdraw must never go negative.", prompt);
        Assert.Contains("Don't test: Message wording.", prompt);
        Assert.Contains("A typo in a printed message is not a mistake.", prompt);
        Assert.Contains("<file name=\"BankAccount.java\">\nclass BankAccount { }\n</file>", prompt);

        var saved = Assert.Single(_testSets.All);
        Assert.Equal(id, saved.Id);
        Assert.Equal((1, TestSetStatus.Draft, TestStyle.ClassDesign, "model"), (saved.Version, saved.Status, saved.Style, saved.Model));
    }

    [Fact]
    public async Task Generate_RenumbersTheAnswer_DropsEmptyCasesAndUnknownRequirements_AndCountsCoverage()
    {
        await Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken);

        var response = (await Service().GetLatestAsync("assignment", TestContext.Current.CancellationToken))!;

        Assert.Equal(["R1", "R2", "R3"], response.Requirements.Select(r => r.Id));
        Assert.Equal([1, 1, 0], response.Requirements.Select(r => r.TestCount));
        Assert.Null(response.Requirements[0].Reason);
        Assert.Equal("Only a reader can judge it.", response.Requirements[2].Reason);
        Assert.Equal(["T1", "T2"], response.Cases.Select(c => c.Id));
        Assert.Equal(["R2"], response.Cases[1].RequirementIds);
        Assert.Equal("100", response.Cases[0].Expected);
        Assert.Null(response.Cases[1].Expected);
        Assert.Equal(TestCategory.Invalid, response.Cases[1].Category);
        Assert.StartsWith("interface Account", response.Interface);
        Assert.Equal("BankAccount.java", Assert.Single(response.Request.ReferenceFiles).Name);
    }

    [Fact]
    public async Task Generate_AgainForTheSameAssignment_MakesTheNextVersion()
    {
        await Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken);
        await Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], _testSets.All.Select(t => t.Version));
        Assert.Equal(2, (await Service().GetLatestAsync("assignment", TestContext.Current.CancellationToken))!.Version);
    }

    [Fact]
    public async Task Generate_ConsoleStyle_KeepsNoInterface()
    {
        await Service().GenerateAsync("course", "assignment", Request() with { Style = TestStyle.Console },
            TestContext.Current.CancellationToken);

        Assert.Null((await Service().GetLatestAsync("assignment", TestContext.Current.CancellationToken))!.Interface);
    }

    [Fact]
    public async Task Generate_WithoutStoredInstructions_FailsBeforeAnyModelCall()
    {
        _grading.Instructions = null;

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken));

        Assert.Null(_model.SystemPrompt);
        Assert.Empty(_testSets.All);
    }

    [Fact]
    public async Task Generate_AnswerWithoutCases_FailsWithAnErrorThatIsNotRetried()
    {
        _model.Reply = """{ "requirements": [], "interface": "", "cases": [] }""";

        await Assert.ThrowsAsync<LanguageModelResponseException>(() =>
            Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken));

        Assert.Empty(_testSets.All);
    }

    [Fact]
    public void Validate_RejectsNoCategoriesAndFilesThatAreNotJava()
    {
        Assert.Throws<InvalidRequestException>(() => TestDesignService.Validate(Request() with { Categories = [] }));
        Assert.Throws<InvalidRequestException>(() => TestDesignService.Validate(
            Request() with { ReferenceFiles = [new ReferenceFile { Name = "notes.txt", Content = "x" }] }));
    }

    [Fact]
    public async Task Save_Draft_EditsInPlace_RenumbersEverything_AndKeepsReferencesPointingAtTheSameRequirements()
    {
        var draft = await GenerateAsync();

        var saved = (await Service().SaveAsync("assignment", draft.Id, new SaveTestSetRequest
        {
            Requirements = [draft.Requirements[1] with { Text = "  Overdraft is refused  " }, draft.Requirements[2]],
            Interface = draft.Interface,
            Cases =
            [
                draft.Cases[1] with { Expected = "throws an exception" },
                new TestCase { Id = "new", Title = "Deposit twice", Category = TestCategory.Normal, RequirementIds = ["R2", "R1"],
                    Steps = "deposit(1)\ndeposit(2)\nbalance()", Expected = "3" },
            ],
        }, TestContext.Current.CancellationToken))!;

        Assert.Equal((draft.Id, 1, TestSetStatus.Draft), (saved.Id, saved.Version, saved.Status));
        Assert.Single(_testSets.All);
        Assert.Equal(["R1", "R2"], saved.Requirements.Select(r => r.Id));
        Assert.Equal("Overdraft is refused", saved.Requirements[0].Text);
        Assert.Equal(["T1", "T2"], saved.Cases.Select(c => c.Id));
        Assert.Equal(["R1"], saved.Cases[0].RequirementIds);
        Assert.Equal(["R1"], saved.Cases[1].RequirementIds);
        Assert.Equal("throws an exception", saved.Cases[0].Expected);
        Assert.Equal([2, 0], saved.Requirements.Select(r => r.TestCount));
    }

    [Fact]
    public async Task Approve_ThenSave_MakesANewDraftVersion_WhileTheApprovedOneStaysInUse()
    {
        var draft = await GenerateAsync();

        var approved = (await Service().ApproveAsync("assignment", draft.Id, TestContext.Current.CancellationToken))!;
        var edited = (await Service().SaveAsync("assignment", draft.Id, Unchanged(draft), TestContext.Current.CancellationToken))!;

        Assert.Equal((TestSetStatus.Approved, 1), (approved.Status, approved.ApprovedVersion));
        Assert.Equal((2, TestSetStatus.Draft, 1), (edited.Version, edited.Status, edited.ApprovedVersion));
        Assert.NotEqual(draft.Id, edited.Id);
        Assert.Equal([TestSetStatus.Approved, TestSetStatus.Draft], _testSets.All.Select(t => t.Status));
    }

    [Fact]
    public async Task Save_AnOlderVersion_IsRejected()
    {
        var first = await GenerateAsync();
        await GenerateAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().SaveAsync("assignment", first.Id, Unchanged(first), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Service().ApproveAsync("assignment", first.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsACaseWithoutSteps_AndASetWithoutCases()
    {
        var draft = await GenerateAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().SaveAsync("assignment", draft.Id,
            Unchanged(draft) with { Cases = [draft.Cases[0] with { Steps = " " }] }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().SaveAsync("assignment", draft.Id,
            Unchanged(draft) with { Cases = [] }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAndApprove_ForAnotherAssignmentOrAnUnknownId_FindNothing()
    {
        var draft = await GenerateAsync();

        Assert.Null(await Service().SaveAsync("other", draft.Id, Unchanged(draft), TestContext.Current.CancellationToken));
        Assert.Null(await Service().ApproveAsync("assignment", Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private static SaveTestSetRequest Unchanged(TestSetResponse testSet) => new()
    {
        Requirements = testSet.Requirements,
        Interface = testSet.Interface,
        Cases = testSet.Cases,
    };

    private async Task<TestSetResponse> GenerateAsync()
    {
        await Service().GenerateAsync("course", "assignment", Request(), TestContext.Current.CancellationToken);
        return (await Service().GetLatestAsync("assignment", TestContext.Current.CancellationToken))!;
    }

    private static GenerateTestsRequest Request() => new()
    {
        Style = TestStyle.ClassDesign,
        Categories = [TestCategory.Normal, TestCategory.Invalid],
        Amount = TestAmount.AboutThreePerCategory,
        Focus = "Withdraw must never go negative.",
        DontTest = "Message wording.",
        ReferenceFiles = [new ReferenceFile { Name = "BankAccount.java", Content = "class BankAccount { }\n" }],
    };

    private TestDesignService Service() => new(_testSets, _grading, new FakeCourseGuidance(), _rules, new FakeAssignmentNotes(),
        _model, new ModelCallContext(), TimeProvider.System);

    private sealed class FakeModel : ILanguageModel
    {
        public string Reply { get; set; } = Answer;

        public string? SystemPrompt { get; private set; }

        public IReadOnlyList<PromptPart>? Parts { get; private set; }

        public string ModelId => "model";

        public string GradingModelId => "model";

        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            Parts = parts;
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
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTestSets : ITestSetStore
    {
        public List<TestSet> All { get; } = [];

        public Task<TestSet?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.Where(t => t.AssignmentId == assignmentId).OrderByDescending(t => t.Version).FirstOrDefault());

        public Task<TestSet?> GetLatestApprovedAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.Where(t => t.AssignmentId == assignmentId && t.Status == TestSetStatus.Approved)
                .OrderByDescending(t => t.Version).FirstOrDefault());

        public Task<TestSet?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(All.FirstOrDefault(t => t.Id == id));

        public void Add(TestSet testSet) => All.Add(testSet);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGrading : IGradingService
    {
        public IReadOnlyList<PromptPart>? Instructions { get; set; } = [new TextPart { Text = "Bank account task" }];

        public Task<IReadOnlyList<PromptPart>?> BuildInstructionsAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Instructions);

        public GradingModelsResponse GetModels() => throw new NotSupportedException();

        public Task<GradingResponse?> TryModelAsync(Guid versionId, string model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EvaluationGrading?> GradeForEvaluationAsync(Guid versionId, string model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GradableVersion?> CheckAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GradeAllPlan> PlanGradeAllAsync(
            string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GradeAllPlan> PlanGradeStudentAsync(
            string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindStudentNameAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
