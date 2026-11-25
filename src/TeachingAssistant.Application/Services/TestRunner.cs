using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Services;

public partial class TestRunner
{
    public const string ReportFile = "reports/TEST-junit-jupiter.xml";

    private const string Schema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "adapter": { "type": "string" },
            "notImplemented": { "type": "array", "items": { "type": "string" } },
            "notes": { "type": "string" }
          },
          "required": ["adapter", "notImplemented", "notes"]
        }
        """;

    private const string StudentSources = "__ta_student_sources.txt";
    private const string AllSources = "__ta_all_sources.txt";
    private const int MaxOutputChars = 6_000;
    private const int MaxMessageChars = 600;
    private const string NotImplementedPrefix = "not implemented";

    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(150);
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly ITestSetStore _testSets;
    private readonly ITestRunStore _runs;
    private readonly ISubmissionStore _submissions;
    private readonly JavaBuildCheck _buildCheck;
    private readonly ISandboxRunner _sandbox;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly TimeProvider _timeProvider;

    public TestRunner(
        ITestSetStore testSets, ITestRunStore runs, ISubmissionStore submissions, JavaBuildCheck buildCheck, ISandboxRunner sandbox,
        ILanguageModel languageModel, ModelCallContext callContext, TimeProvider timeProvider)
    {
        _testSets = testSets;
        _runs = runs;
        _submissions = submissions;
        _buildCheck = buildCheck;
        _sandbox = sandbox;
        _languageModel = languageModel;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<TestRun> RunAsync(Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken)
                      ?? throw new InvalidRequestException("The test set no longer exists.");
        var content = TestDesignService.ReadContent(testSet);
        EnsureCanRun(testSet, content);
        var submission = await _submissions.FindByVersionAsync(versionId, cancellationToken)
                         ?? throw new InvalidRequestException("The submission version no longer exists.");
        return await RunAsync(testSet, content, submission, versionId, newAdapter, cancellationToken);
    }

    public async Task<GradingTests?> RunForGradingAsync(Submission submission, Guid versionId, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.GetLatestApprovedAsync(submission.AssignmentId, cancellationToken);
        if (testSet is null || testSet.Style != TestStyle.ClassDesign)
        {
            return null;
        }

        var content = TestDesignService.ReadContent(testSet);
        if (content.Code is not { Status: not TestCodeStatus.CompileFailed })
        {
            return null;
        }

        var run = (await _runs.GetLatestByTestSetAsync(testSet.Id, cancellationToken))
            .FirstOrDefault(r => r.VersionId == versionId && r.Status != TestRunStatus.NotRun);
        var tests = new GradingTests
        {
            TestSetVersion = testSet.Version,
            Status = TestRunStatus.HarnessFailed,
            Requirements = content.Requirements,
            Cases = content.Cases,
            Results = [],
        };
        if (run is null)
        {
            try
            {
                run = await RunAsync(testSet, content, submission, versionId, false, cancellationToken);
            }
            catch (LanguageModelResponseException)
            {
                return tests;
            }
        }

        return run.Status == TestRunStatus.NotRun ? null : tests with { Status = run.Status, Results = ReadDetails(run).Results };
    }

    internal static string? GradingSection(GradingTests tests)
    {
        if (tests.Status != TestRunStatus.Completed || tests.Cases.Count == 0)
        {
            return null;
        }

        var results = tests.Results.ToDictionary(r => r.CaseId, StringComparer.OrdinalIgnoreCase);
        var passed = tests.Cases.Count(c => results.GetValueOrDefault(c.Id)?.Outcome == TestOutcome.Passed);
        var used = tests.Cases.SelectMany(c => c.RequirementIds).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var section = new StringBuilder("<automated_tests>\n")
            .Append("Test set version ").Append(tests.TestSetVersion).Append(", approved by the teacher; ")
            .Append(passed).Append(" of ").Append(tests.Cases.Count).Append(" cases passed.\n\nRequirements:\n");
        foreach (var requirement in tests.Requirements.Where(r => used.Contains(r.Id)))
        {
            section.Append(requirement.Id).Append(": ").Append(requirement.Text).Append('\n');
        }

        section.Append("\nCases:\n");
        foreach (var testCase in tests.Cases)
        {
            var result = results.GetValueOrDefault(testCase.Id);
            var outcome = result?.Outcome ?? TestOutcome.Missing;
            section.Append(testCase.Id).Append(' ').Append(testCase.Title);
            if (testCase.RequirementIds.Count > 0)
            {
                section.Append(" [").Append(string.Join(", ", testCase.RequirementIds)).Append(']');
            }

            section.Append(": ").Append(OutcomeText(outcome)).Append('\n');
            if (outcome == TestOutcome.Passed)
            {
                continue;
            }

            section.Append("  Steps: ").Append(testCase.Steps.ReplaceLineEndings("; ")).Append('\n');
            if (!string.IsNullOrWhiteSpace(testCase.Expected))
            {
                section.Append("  Expected: ").Append(testCase.Expected.ReplaceLineEndings("; ")).Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(result?.Message))
            {
                section.Append("  Result: ").Append(result.Message.ReplaceLineEndings(" ")).Append('\n');
            }
        }

        return section.Append("</automated_tests>").ToString();
    }

    internal static string SentLabel(GradingTests tests)
    {
        return tests.Status switch
        {
            TestRunStatus.Completed =>
                $"Tests: version {tests.TestSetVersion}, {tests.Results.Count(r => r.Outcome == TestOutcome.Passed)} of {tests.Cases.Count} passed",
            TestRunStatus.StudentCompileFailed => $"Tests: version {tests.TestSetVersion} not run (the code does not compile)",
            _ => $"Tests: version {tests.TestSetVersion} not used (the adapter did not fit this submission)",
        };
    }

    private static string OutcomeText(TestOutcome outcome)
    {
        return outcome switch
        {
            TestOutcome.Passed => "passed",
            TestOutcome.Failed => "FAILED",
            TestOutcome.NotImplemented => "not implemented (the adapter found no part of the code that does this)",
            _ => "did not run",
        };
    }

    private async Task<TestRun> RunAsync(
        TestSet testSet, TestSetContent content, Submission submission, Guid versionId, bool newAdapter, CancellationToken cancellationToken)
    {
        var version = submission.Versions.Single(v => v.Id == versionId);
        _callContext.Describe(submission.CourseId, submission.AssignmentId, versionId);

        var sources = _buildCheck.ReadSources(version).Select(s => new SandboxFile { Path = s.Path, Content = s.Bytes }).ToList();
        var run = new RunState { TestSet = testSet, Submission = submission, VersionId = versionId, Content = content };
        if (sources.Count == 0)
        {
            return await SaveAsync(run, TestRunStatus.NotRun, output: "The submission has no Java source files.", cancellationToken: cancellationToken);
        }

        try
        {
            var compile = await CompileStudentAsync(sources, cancellationToken);
            if (compile is not null)
            {
                return await SaveAsync(run, TestRunStatus.StudentCompileFailed, output: compile, cancellationToken: cancellationToken);
            }

            var reused = newAdapter ? null : await _runs.FindLatestWithAdapterAsync(versionId, cancellationToken);
            var adapter = reused is { Adapter: { } stored } && reused.Interface == content.Interface
                ? new WrittenAdapter { Source = stored, Model = reused.AdapterModel, Details = ReadDetails(reused) }
                : await WriteAdapterAsync(content, sources, null, cancellationToken);

            var result = await RunTestsAsync(sources, content, adapter.Source, cancellationToken);
            if (result.CompileErrors is { } errors)
            {
                adapter = await WriteAdapterAsync(content, sources, (adapter.Source, errors), cancellationToken);
                result = await RunTestsAsync(sources, content, adapter.Source, cancellationToken);
                if (result.CompileErrors is { } again)
                {
                    return await SaveAsync(run, TestRunStatus.HarnessFailed, adapter,
                        "The adapter did not compile with this submission, so no test result counts against the student.\n\n" + again,
                        cancellationToken: cancellationToken);
                }
            }

            return await SaveAsync(run, TestRunStatus.Completed, adapter, result.Output, result.Results, cancellationToken);
        }
        catch (SandboxUnavailableException ex)
        {
            return await SaveAsync(run, TestRunStatus.NotRun, output: ex.Message, cancellationToken: cancellationToken);
        }
    }

    public static void EnsureCanRun(TestSet testSet, TestSetContent content)
    {
        TestCodeService.EnsureCanHaveCode(testSet);
        if (content.Code is not { } code)
        {
            throw new InvalidRequestException("The test set has no test code yet. Write it on the Tests view first.");
        }

        if (code.Status == TestCodeStatus.CompileFailed)
        {
            throw new InvalidRequestException("The test code does not compile. Write it again before running it on students.");
        }
    }

    internal static string AdapterPrompt(TestSetContent content, IReadOnlyList<SandboxFile> sources, (string Adapter, string Errors)? repair)
    {
        var prompt = new StringBuilder();
        prompt.Append("<interface file=\"").Append(content.Code!.InterfaceFile).AppendLine("\">");
        prompt.AppendLine(content.Interface!.TrimEnd());
        prompt.AppendLine("</interface>").AppendLine();

        prompt.AppendLine("<requirements>");
        foreach (var requirement in content.Requirements.Where(r => r.Testable))
        {
            prompt.Append(requirement.Id).Append(": ").AppendLine(requirement.Text);
        }

        prompt.AppendLine("</requirements>").AppendLine();

        prompt.AppendLine("<student_code>");
        foreach (var file in sources)
        {
            prompt.Append("<file path=\"").Append(file.Path).AppendLine("\">");
            prompt.AppendLine(Utf8.GetString(file.Content).TrimEnd());
            prompt.AppendLine("</file>");
        }

        prompt.AppendLine("</student_code>");

        if (repair is { } earlier)
        {
            prompt.AppendLine().AppendLine("<previous_adapter>").AppendLine(earlier.Adapter.TrimEnd()).AppendLine("</previous_adapter>");
            prompt.AppendLine().AppendLine("<compiler_errors>").AppendLine(earlier.Errors.TrimEnd()).AppendLine("</compiler_errors>");
            prompt.AppendLine().Append("The previous adapter did not compile. Write a corrected StudentAdapter.");
        }
        else
        {
            prompt.AppendLine().Append("Write the StudentAdapter for this student's code.");
        }

        return prompt.ToString().ReplaceLineEndings("\n");
    }

    internal static IReadOnlyList<TestCaseResult> ReadReport(string xml, IReadOnlyList<TestCase> cases)
    {
        var found = new Dictionary<string, TestCaseResult>(StringComparer.OrdinalIgnoreCase);
        XDocument report;
        try
        {
            report = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return Missing(cases, "The test report could not be read.");
        }

        foreach (var testCase in report.Descendants("testcase"))
        {
            var name = (string?)testCase.Attribute("name") ?? "";
            var match = CaseName().Match(name);
            if (!match.Success)
            {
                continue;
            }

            var id = "T" + match.Groups["n"].Value;
            found.TryAdd(id, Read(id, testCase));
        }

        return cases.Select(c => found.GetValueOrDefault(c.Id)
                                 ?? new TestCaseResult { CaseId = c.Id, Outcome = TestOutcome.Missing, Message = "This case did not run." })
            .ToList();
    }

    private static TestCaseResult Read(string id, XElement testCase)
    {
        if (testCase.Element("failure") is { } failure)
        {
            return new TestCaseResult { CaseId = id, Outcome = TestOutcome.Failed, Message = Message(failure) };
        }

        if (testCase.Element("error") is { } error)
        {
            var type = (string?)error.Attribute("type") ?? "";
            var message = (string?)error.Attribute("message") ?? "";
            if (type.EndsWith("UnsupportedOperationException", StringComparison.Ordinal)
                && message.StartsWith(NotImplementedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new TestCaseResult { CaseId = id, Outcome = TestOutcome.NotImplemented, Message = Limit(message) };
            }

            if (type.EndsWith("TimeoutException", StringComparison.Ordinal))
            {
                return new TestCaseResult
                {
                    CaseId = id,
                    Outcome = TestOutcome.Failed,
                    Message = "The student's code did not finish within 5 seconds (an endless loop?).",
                };
            }

            return new TestCaseResult { CaseId = id, Outcome = TestOutcome.Failed, Message = Limit($"{ShortType(type)}: {message}".TrimEnd(':', ' ')) };
        }

        return testCase.Element("skipped") is not null
            ? new TestCaseResult { CaseId = id, Outcome = TestOutcome.Missing, Message = "This case was skipped." }
            : new TestCaseResult { CaseId = id, Outcome = TestOutcome.Passed };
    }

    private static string Message(XElement element)
    {
        return Limit((string?)element.Attribute("message") is { Length: > 0 } message ? message : ShortType((string?)element.Attribute("type") ?? ""));
    }

    private static string ShortType(string type)
    {
        return type[(type.LastIndexOf('.') + 1)..];
    }

    private static string Limit(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > MaxMessageChars ? trimmed[..MaxMessageChars] + "…" : trimmed;
    }

    private static List<TestCaseResult> Missing(IReadOnlyList<TestCase> cases, string message)
    {
        return cases.Select(c => new TestCaseResult { CaseId = c.Id, Outcome = TestOutcome.Missing, Message = message }).ToList();
    }

    private static string Trim(string output)
    {
        var trimmed = output.Trim();
        return trimmed.Length > MaxOutputChars ? trimmed[..MaxOutputChars] + "\n…" : trimmed;
    }

    private static SandboxFile ArgumentFile(string name, IEnumerable<string> paths)
    {
        return new SandboxFile { Path = name, Content = Utf8.GetBytes(string.Join("\n", paths.Select(p => $"\"{p}\"")) + "\n") };
    }

    internal static TestRunDetails ReadDetails(TestRun run)
    {
        return JsonSerializer.Deserialize<TestRunDetails>(run.Results, TestDesignService.JsonOptions)
               ?? new TestRunDetails { Results = [] };
    }

    private async Task<string?> CompileStudentAsync(IReadOnlyList<SandboxFile> sources, CancellationToken cancellationToken)
    {
        var result = await _sandbox.RunAsync(new SandboxRequest
        {
            Image = JavaBuildCheck.Image,
            Files = [.. sources, ArgumentFile(StudentSources, sources.Select(s => s.Path))],
            Steps =
            [
                new SandboxStep
                {
                    Name = "compile submission",
                    Command = ["javac", "-encoding", "UTF-8", "-nowarn", "-d", "student", "@" + StudentSources],
                    Timeout = CompileTimeout,
                },
            ],
        }, cancellationToken);
        var step = result.Steps.Single();
        return step.Outcome == SandboxStepOutcome.Succeeded ? null : Trim(step.Stderr + step.Stdout);
    }

    private async Task<WrittenAdapter> WriteAdapterAsync(
        TestSetContent content, IReadOnlyList<SandboxFile> sources, (string Adapter, string Errors)? repair, CancellationToken cancellationToken)
    {
        var reply = await _languageModel.WriteAdapterAsync(PromptLibrary.TestAdapter,
            [new TextPart { Text = AdapterPrompt(content, sources, repair) }], Schema, cancellationToken);
        AdapterReply answer;
        try
        {
            answer = JsonSerializer.Deserialize<AdapterReply>(reply.Text, TestDesignService.JsonOptions)
                     ?? throw new LanguageModelResponseException("The adapter came back empty.");
        }
        catch (JsonException ex)
        {
            throw new LanguageModelResponseException("The adapter answer could not be read.", ex);
        }

        if (string.IsNullOrWhiteSpace(answer.Adapter) || !answer.Adapter.Contains("class StudentAdapter", StringComparison.Ordinal))
        {
            throw new LanguageModelResponseException("The answer has no StudentAdapter class.");
        }

        return new WrittenAdapter
        {
            Source = answer.Adapter.Trim() + "\n",
            Model = reply.Model,
            Details = new TestRunDetails
            {
                Results = [],
                Unmapped = (answer.NotImplemented ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList(),
                Notes = string.IsNullOrWhiteSpace(answer.Notes) ? null : answer.Notes.Trim(),
            },
        };
    }

    private async Task<TestResult> RunTestsAsync(
        IReadOnlyList<SandboxFile> sources, TestSetContent content, string adapter, CancellationToken cancellationToken)
    {
        var code = content.Code!;
        var harness = new[]
        {
            new SandboxFile { Path = code.InterfaceFile, Content = Utf8.GetBytes(content.Interface!.TrimEnd() + "\n") },
            new SandboxFile { Path = TestCodeService.AdapterFile, Content = Utf8.GetBytes(adapter) },
            new SandboxFile { Path = TestCodeService.TestClassFile, Content = Utf8.GetBytes(code.TestClass) },
        };
        var all = sources.Concat(harness).ToList();
        var result = await _sandbox.RunAsync(new SandboxRequest
        {
            Image = JavaBuildCheck.Image,
            Files = [.. all, ArgumentFile(AllSources, all.Select(f => f.Path))],
            Steps =
            [
                new SandboxStep
                {
                    Name = "compile",
                    Command = ["javac", "-encoding", "UTF-8", "-nowarn", "-d", "out", "-cp", TestCodeService.JUnitJar, "@" + AllSources],
                    Timeout = CompileTimeout,
                    StopOnFailure = true,
                },
                new SandboxStep
                {
                    Name = "test",
                    Command = ["java", "-jar", TestCodeService.JUnitJar, "execute", "--class-path", "out", "--select-class", "AssignmentTests",
                        "--reports-dir", "reports", "--details", "none", "--disable-banner"],
                    Timeout = TestTimeout,
                },
                new SandboxStep { Name = "report", Command = ["cat", ReportFile] },
            ],
        }, cancellationToken);

        var compile = result.Steps[0];
        if (compile.Outcome != SandboxStepOutcome.Succeeded)
        {
            return new TestResult { CompileErrors = Trim(compile.Stderr + compile.Stdout), Results = [] };
        }

        var test = result.Steps[1];
        var report = result.Steps[2];
        var cases = content.Cases;
        if (report.Outcome != SandboxStepOutcome.Succeeded || string.IsNullOrWhiteSpace(report.Stdout))
        {
            var why = test.Outcome == SandboxStepOutcome.TimedOut
                ? "The tests did not finish in time."
                : "The tests ran but wrote no report.";
            return new TestResult { Results = Missing(cases, why), Output = Trim($"{why}\n{test.Stdout}{test.Stderr}") };
        }

        return new TestResult { Results = ReadReport(report.Stdout, cases) };
    }

    private async Task<TestRun> SaveAsync(
        RunState run, TestRunStatus status, WrittenAdapter? adapter = null, string? output = null, IReadOnlyList<TestCaseResult>? results = null,
        CancellationToken cancellationToken = default)
    {
        var details = (adapter?.Details ?? new TestRunDetails { Results = [] }) with { Results = results ?? [] };
        var saved = new TestRun(run.TestSet.Id, run.TestSet.Version, run.VersionId, run.Submission.CourseId, run.Submission.AssignmentId,
            run.Submission.StudentId, status, run.Content.Interface, adapter?.Source, adapter?.Model,
            JsonSerializer.Serialize(details, TestDesignService.JsonOptions), output, _timeProvider.GetUtcNow());
        _runs.Add(saved);
        await _runs.SaveChangesAsync(cancellationToken);
        return saved;
    }

    [GeneratedRegex(@"^\s*[tT](?<n>\d+)[_\s]")]
    private static partial Regex CaseName();

    private sealed record RunState
    {
        public required TestSet TestSet { get; init; }
        public required Submission Submission { get; init; }
        public required Guid VersionId { get; init; }
        public required TestSetContent Content { get; init; }
    }

    private sealed record WrittenAdapter
    {
        public required string Source { get; init; }
        public string? Model { get; init; }
        public required TestRunDetails Details { get; init; }
    }

    private sealed record TestResult
    {
        public string? CompileErrors { get; init; }
        public required IReadOnlyList<TestCaseResult> Results { get; init; }
        public string? Output { get; init; }
    }

    private sealed record AdapterReply
    {
        public string? Adapter { get; init; }
        public List<string>? NotImplemented { get; init; }
        public string? Notes { get; init; }
    }
}