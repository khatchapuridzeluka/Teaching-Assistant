using System.Reflection;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Tests.Application;

public class TestToolsTests
{
    private readonly FakeDesign _design = new();
    private readonly FakeRuns _runs = new();
    private readonly FakeTestSets _testSets = new();
    private readonly FakeSubmissions _submissions = new();
    private readonly FakeTestCodeJobs _jobs = new();
    private readonly AgentRunContext _context = new();

    private TestTools Tools()
    {
        return new TestTools(_design, _runs, _testSets, _submissions, _jobs, _context, TimeProvider.System);
    }

    [Fact]
    public void TheToolsAreMarked()
    {
        var names = typeof(TestTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["generate_test_cases", "get_test_cases", "get_test_results", "run_tests", "run_tests_for_student"], names);
    }

    [Fact]
    public async Task Generate_StartsFromThePreviousSettings_AndOverridesWhatIsGiven()
    {
        _design.Latest = SetResponse(2, TestSetStatus.Approved, approvedVersion: 2) with
        {
            Request = new GenerateTestsRequest
            {
                Style = TestStyle.Console,
                Categories = [TestCategory.Boundary],
                Amount = TestAmount.AboutThreePerCategory,
                Focus = "old focus",
                DontTest = "message wording",
                ReferenceFiles = [new ReferenceFile { Name = "Main.java", Content = "class Main { }" }],
            },
        };

        var result = await Tools().GenerateTestCasesAsync("course", "assignment", focus: "edge cases",
            cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(_jobs.GenerateJobs);
        Assert.Equal(TestStyle.Console, request.Style);
        Assert.Equal([TestCategory.Boundary], request.Categories);
        Assert.Equal("edge cases", request.Focus);
        Assert.Equal("message wording", request.DontTest);
        Assert.Single(request.ReferenceFiles);
        Assert.Contains("It becomes version 3; version 2 (approved) stays in the history.", result);
        Assert.Contains("you cannot approve them", result);
        Assert.NotNull(_context.TakeQueuedJob());
        Assert.Equal(AgentLinkKind.Tests, Assert.Single(_context.TakeLinks()).Kind);
    }

    [Fact]
    public async Task Generate_UsesTheDefaults_WhenThereIsNoPreviousSet()
    {
        await Tools().GenerateTestCasesAsync("course", "assignment", cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(_jobs.GenerateJobs);
        Assert.Equal(TestStyle.ClassDesign, request.Style);
        Assert.Equal([TestCategory.Normal, TestCategory.Boundary, TestCategory.Invalid], request.Categories);
        Assert.Equal(TestAmount.AboutTenInTotal, request.Amount);
        Assert.Empty(request.ReferenceFiles);
    }

    [Fact]
    public async Task Generate_RefusesAnUnknownCategory()
    {
        var result = await Tools().GenerateTestCasesAsync("course", "assignment", categories: ["Normal", "Weird"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("Not started: 'Weird' is not a kind of test case.", result);
        Assert.Empty(_jobs.GenerateJobs);
    }

    [Fact]
    public async Task GetTestCases_ListsTheCases_AndSaysWhichVersionGradingsUse()
    {
        _design.Latest = SetResponse(2, TestSetStatus.Draft, approvedVersion: 1);

        var result = await Tools().GetTestCasesAsync("course", "assignment", TestContext.Current.CancellationToken);

        Assert.StartsWith("Test set version 2, draft, ClassDesign style. Gradings use the approved version 1;", result);
        Assert.Contains("R1: Deposit adds to the balance (1 cases)", result);
        Assert.Contains("T1 Deposit [Normal; R1]\n  Steps: deposit(5); balance()\n  Expected: 5", result);
        Assert.Contains("interface WalletHomework", result);
        Assert.EndsWith("Test code: not written yet (it is written after the teacher approves the cases).", result);
    }

    [Fact]
    public async Task GetTestCases_PointsToGenerate_WhenThereAreNone()
    {
        var result = await Tools().GetTestCasesAsync("course", "assignment", TestContext.Current.CancellationToken);

        Assert.StartsWith("This assignment has no test cases yet.", result);
        Assert.Empty(_context.TakeLinks());
    }

    [Fact]
    public async Task RunTests_QueuesEveryStudentWithWork_OnTheApprovedSet()
    {
        var approved = AddSet(1, approve: true);
        AddSet(2, approve: false);
        var ana = AddSubmission("student-1", "Ana Beridze", withVersion: true);
        AddSubmission("student-2", "Nino Gelashvili", withVersion: false);

        var result = await Tools().RunTestsAsync("course", "assignment", TestContext.Current.CancellationToken);

        var queued = Assert.Single(_runs.Queued);
        Assert.Equal((approved.Id, ana.LatestVersion!.Id, false), queued);
        Assert.StartsWith($"Test runs of version 1 queued for 1 students (job ids {_runs.JobIds[0]}). 1 students have no stored work", result);
        Assert.DoesNotContain("Ana", result);
        Assert.DoesNotContain("Nino", result);
    }

    [Fact]
    public async Task RunTests_WithoutAnApprovedSet_SaysTheTeacherApprovesFirst()
    {
        AddSet(1, approve: false);
        AddSubmission("student-1", "Ana Beridze", withVersion: true);

        var result = await Tools().RunTestsAsync("course", "assignment", TestContext.Current.CancellationToken);

        Assert.StartsWith("This assignment has no approved test set", result);
        Assert.Empty(_runs.Queued);
    }

    [Fact]
    public async Task RunTestsForStudent_PassesNewAdapter_AndRecordsTheJob()
    {
        var approved = AddSet(1, approve: true);
        AddSubmission("student-1", "Ana Beridze", withVersion: true);
        var nino = AddSubmission("student-2", "Nino Gelashvili", withVersion: true);

        var result = await Tools().RunTestsForStudentAsync("course", "assignment", "student-2", newAdapter: true,
            TestContext.Current.CancellationToken);

        Assert.Equal([(approved.Id, nino.LatestVersion!.Id, true)], _runs.Queued);
        Assert.Contains("with a newly written adapter", result);
        Assert.Equal(_runs.JobIds[0], _context.TakeQueuedJob());
    }

    [Fact]
    public async Task GetTestResults_ShowsWhatDidNotPass_ByStudentId_WithoutNames()
    {
        AddSet(1, approve: true);
        _runs.Latest =
        [
            RunResponse("student-1", 1, TestRunStatus.Completed,
                [Result("T1", TestOutcome.Passed, null), Result("T2", TestOutcome.Failed, "expected: <10> but was: <5>")]),
            RunResponse("student-1", 2, TestRunStatus.Completed, [Result("T1", TestOutcome.Passed, null), Result("T2", TestOutcome.Passed, null)]),
            RunResponse("student-2", 1, TestRunStatus.Completed,
                [Result("T1", TestOutcome.NotImplemented, "deposit"), Result("T2", TestOutcome.Failed, "expected: <10>\nbut was: <0>")]),
            RunResponse("student-3", 1, TestRunStatus.HarnessFailed, []),
        ];

        var result = await Tools().GetTestResultsAsync("course", "assignment", cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("Results of test set version 1 (2 cases):", result);
        Assert.Contains("Student student-1, version 2: 2 of 2 passed.", result);
        Assert.Contains("Student student-2, version 1: 0 of 2 passed.\n  T1 Deposit not implemented: deposit\n  T2 Withdraw failed: expected: <10>; but was: <0>", result);
        Assert.Contains("Student student-3, version 1: the adapter did not fit this submission", result);
        Assert.DoesNotContain("Student student-1, version 1", result);
        Assert.DoesNotContain("Name", result);
    }

    [Fact]
    public async Task GetTestResults_ForOneStudent_LeavesTheOthersOut()
    {
        AddSet(1, approve: true);
        _runs.Latest =
        [
            RunResponse("student-1", 1, TestRunStatus.StudentCompileFailed, []),
            RunResponse("student-2", 1, TestRunStatus.Completed, [Result("T1", TestOutcome.Passed, null)]),
        ];

        var result = await Tools().GetTestResultsAsync("course", "assignment", "student-1", TestContext.Current.CancellationToken);

        Assert.Contains("Student student-1, version 1: not run, the student's code does not compile.", result);
        Assert.DoesNotContain("student-2", result);
    }

    private TestSet AddSet(int version, bool approve)
    {
        var request = new GenerateTestsRequest
        {
            Style = TestStyle.ClassDesign,
            Categories = [TestCategory.Normal],
            Amount = TestAmount.AboutTenInTotal,
            ReferenceFiles = [],
        };
        var content = new TestSetContent { Requirements = Requirements(), Interface = Interface, Cases = Cases() };
        var testSet = new TestSet("course", "assignment", version, TestStyle.ClassDesign,
            JsonSerializer.Serialize(request, TestDesignService.JsonOptions), JsonSerializer.Serialize(content, TestDesignService.JsonOptions),
            "model", DateTimeOffset.UtcNow);
        if (approve)
        {
            testSet.Approve();
        }

        _testSets.Add(testSet);
        return testSet;
    }

    private Submission AddSubmission(string studentId, string name, bool withVersion)
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "course", "assignment", studentId, name, "sub-" + studentId, "folder",
            DateTimeOffset.UtcNow);
        if (withVersion)
        {
            submission.AddVersion(VersionStatus.Ready, null, "hash", "folder/v1", null, DateTimeOffset.UtcNow, []);
        }

        _submissions.All.Add(submission);
        return submission;
    }

    private const string Interface = "public interface WalletHomework {\n    void deposit(int amount);\n}";

    private static List<TestRequirement> Requirements()
    {
        return [new TestRequirement { Id = "R1", Text = "Deposit adds to the balance", Testable = true, TestCount = 1 }];
    }

    private static List<TestCase> Cases()
    {
        return
        [
            new TestCase
            {
                Id = "T1",
                Title = "Deposit",
                Category = TestCategory.Normal,
                RequirementIds = ["R1"],
                Steps = "deposit(5)\nbalance()",
                Expected = "5",
            },
            new TestCase
            {
                Id = "T2",
                Title = "Withdraw",
                Category = TestCategory.Normal,
                RequirementIds = ["R1"],
                Steps = "withdraw(5)",
                Expected = "0",
            },
        ];
    }

    private static TestSetResponse SetResponse(int version, TestSetStatus status, int? approvedVersion)
    {
        return new TestSetResponse
        {
            Id = Guid.NewGuid(),
            Version = version,
            Status = status,
            Style = TestStyle.ClassDesign,
            Request = new GenerateTestsRequest
            {
                Style = TestStyle.ClassDesign,
                Categories = [TestCategory.Normal],
                Amount = TestAmount.AboutTenInTotal,
                ReferenceFiles = [],
            },
            Requirements = Requirements(),
            Interface = Interface,
            Cases = [Cases()[0]],
            ApprovedVersion = approvedVersion,
            Model = "model",
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static TestRunResponse RunResponse(string studentId, int versionNumber, TestRunStatus status, IReadOnlyList<TestCaseResult> results)
    {
        return new TestRunResponse
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            VersionNumber = versionNumber,
            StudentId = studentId,
            StudentName = "Name of " + studentId,
            TestSetVersion = 1,
            Status = status,
            Results = results,
            Passed = results.Count(r => r.Outcome == TestOutcome.Passed),
            Failed = results.Count(r => r.Outcome == TestOutcome.Failed),
            NotImplemented = results.Count(r => r.Outcome == TestOutcome.NotImplemented),
            Unmapped = [],
            Output = status == TestRunStatus.Completed ? null : "it broke",
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static TestCaseResult Result(string caseId, TestOutcome outcome, string? message)
    {
        return new TestCaseResult { CaseId = caseId, Outcome = outcome, Message = message };
    }

    private sealed class FakeDesign : ITestDesignService
    {
        public TestSetResponse? Latest { get; set; }

        public Task<Guid> GenerateAsync(
            string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TestSetResponse?> GetLatestAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Latest);

        public Task<TestSetResponse?> SaveAsync(
            string assignmentId, Guid testSetId, SaveTestSetRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TestSetResponse?> ApproveAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> QueueCodeAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRuns : ITestRunService
    {
        public List<(Guid TestSetId, Guid VersionId, bool NewAdapter)> Queued { get; } = [];

        public List<Guid> JobIds { get; } = [];

        public IReadOnlyList<TestRunResponse> Latest { get; set; } = [];

        public Task RunAsync(Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RunTestsResponse?> QueueAllAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> QueueOneAsync(
            string assignmentId, Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default)
        {
            Queued.Add((testSetId, versionId, newAdapter));
            var job = JobPayloads.ToResponse(new Job(JobType.RunTests,
                JobPayloads.Write(new RunTestsPayload { TestSetId = testSetId, VersionId = versionId }), DateTimeOffset.UtcNow));
            JobIds.Add(job.Id);
            return Task.FromResult<JobResponse?>(job);
        }

        public Task<IReadOnlyList<TestRunResponse>?> GetLatestAsync(
            string assignmentId, Guid testSetId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TestRunResponse>?>(Latest);
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
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => All.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
