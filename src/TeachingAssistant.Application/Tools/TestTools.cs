using System.ComponentModel;
using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Tools;

public class TestTools
{
    internal const int MaxMessageLength = 300;
    internal const int MaxInterfaceLength = 6_000;

    private const string NoApprovedSet =
        "This assignment has no approved test set, so there is nothing to run or read. The teacher approves the test cases on the "
        + "Tests view; if there are none yet, generate_test_cases writes them (one paid AI call, ask first).";

    private static readonly IReadOnlyList<TestCategory> DefaultCategories = [TestCategory.Normal, TestCategory.Boundary, TestCategory.Invalid];

    private readonly ITestDesignService _design;
    private readonly ITestRunService _runs;
    private readonly ITestSetStore _testSets;
    private readonly ISubmissionStore _submissions;
    private readonly IJobService _jobs;
    private readonly AgentRunContext _context;
    private readonly TimeProvider _timeProvider;

    public TestTools(
        ITestDesignService design, ITestRunService runs, ITestSetStore testSets, ISubmissionStore submissions, IJobService jobs,
        AgentRunContext context, TimeProvider timeProvider)
    {
        _design = design;
        _runs = runs;
        _testSets = testSets;
        _submissions = submissions;
        _jobs = jobs;
        _context = context;
        _timeProvider = timeProvider;
    }

    [AgentTool("generate_test_cases")]
    [Description(
        "Starts writing a new version of an assignment's automated test cases from its instructions (like Generate on the Tests "
        + "view): one paid AI call that lists the requirements and writes the cases, and for class-design tests a neutral Java "
        + "interface they call. It runs in the background and returns a job id at once. The new version is a draft: the teacher "
        + "reviews, edits and approves it on the Tests view, and only an approved version is used in gradings and test runs. "
        + "Settings left out are taken from the assignment's previous test set (its reference solution included), or the defaults "
        + "when there is none. Use it only when the teacher asks for test cases.")]
    public async Task<string> GenerateTestCasesAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("ClassDesign (the tests call the student's classes through an interface; the usual choice for OOP homework) "
            + "or Console (input on standard input, expected output)")] string? style = null,
        [Description("The kinds of cases to write, from Normal, Boundary, Invalid, LargeInput and Concurrency; default Normal, "
            + "Boundary and Invalid")] string[]? categories = null,
        [Description("How many cases: AboutThreePerCategory, AboutTenInTotal (the default) or FewestThatCoverEveryRequirement")]
        string? amount = null,
        [Description("What the teacher wants checked in particular, in their words")] string? focus = null,
        [Description("What the tests must not check, such as the exact wording of printed messages")] string? dontTest = null,
        CancellationToken cancellationToken = default)
    {
        var previous = await _design.GetLatestAsync(assignmentId, cancellationToken);
        var basis = previous?.Request;

        var parsedStyle = basis?.Style ?? TestStyle.ClassDesign;
        if (style is not null && !TryParse(style, out parsedStyle))
        {
            return $"Not started: '{style}' is not a test style. Use ClassDesign or Console.";
        }

        var parsedCategories = basis?.Categories ?? DefaultCategories;
        if (categories is { Length: > 0 })
        {
            var list = new List<TestCategory>();
            foreach (var text in categories)
            {
                if (!TryParse<TestCategory>(text, out var category))
                {
                    return $"Not started: '{text}' is not a kind of test case. Use Normal, Boundary, Invalid, LargeInput or Concurrency.";
                }

                list.Add(category);
            }

            parsedCategories = list.Distinct().ToList();
        }

        var parsedAmount = basis?.Amount ?? TestAmount.AboutTenInTotal;
        if (amount is not null && !TryParse(amount, out parsedAmount))
        {
            return $"Not started: '{amount}' is not an amount. Use AboutThreePerCategory, AboutTenInTotal or FewestThatCoverEveryRequirement.";
        }

        var request = new GenerateTestsRequest
        {
            Style = parsedStyle,
            Categories = parsedCategories,
            Amount = parsedAmount,
            Focus = focus ?? basis?.Focus,
            DontTest = dontTest ?? basis?.DontTest,
            ReferenceFiles = basis?.ReferenceFiles ?? [],
        };

        try
        {
            var started = _timeProvider.GetUtcNow();
            var job = await _jobs.QueueGenerateTestsAsync(courseId, assignmentId, request, cancellationToken);
            _context.RecordQueuedJob(job.Id);
            _context.RecordLinks([Link(courseId, assignmentId)]);
            if (job.CreatedAt < started)
            {
                return $"Test cases for this assignment are already being written (job id {job.Id}); nothing new was started.";
            }

            var replaces = previous is null
                ? ""
                : $" It becomes version {previous.Version + 1}; version {previous.Version} ({previous.Status.ToString().ToLowerInvariant()}) stays in the history.";
            var reference = request.ReferenceFiles.Count > 0 ? $" It uses the teacher's reference solution ({request.ReferenceFiles.Count} files)." : "";
            return $"Test case generation queued (job id {job.Id}): {parsedStyle} tests, {string.Join(", ", parsedCategories)}, "
                + $"{parsedAmount}.{replaces}{reference} It usually takes a minute or two. Then the teacher reviews and approves the "
                + "cases on the Tests view; you cannot approve them.";
        }
        catch (InvalidRequestException ex)
        {
            return $"Not started: {ex.Message}";
        }
    }

    [AgentTool("get_test_cases")]
    [Description(
        "Reads the latest version of an assignment's automated test cases: whether it is a draft or approved, which version "
        + "gradings use, the requirements, every case (id, title, steps, expected result) and the state of the test code. It is "
        + "free. Use it when the teacher asks about the tests, or to explain what a failed case (T1, T2, …) checks.")]
    public async Task<string> GetTestCasesAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        CancellationToken cancellationToken = default)
    {
        var set = await _design.GetLatestAsync(assignmentId, cancellationToken);
        if (set is null)
        {
            return "This assignment has no test cases yet. generate_test_cases writes them (one paid AI call); ask the teacher first.";
        }

        _context.RecordLinks([Link(courseId, assignmentId)]);
        var text = new StringBuilder();
        text.Append($"Test set version {set.Version}, {set.Status.ToString().ToLowerInvariant()}, {set.Style} style. ");
        text.AppendLine(set.ApprovedVersion switch
        {
            null => "No version is approved yet, so gradings do not use tests; the teacher approves on the Tests view.",
            var approved when approved == set.Version => "Gradings use this version.",
            var approved => $"Gradings use the approved version {approved}; this newer draft waits for the teacher's approval.",
        });

        text.AppendLine().AppendLine("Requirements:");
        foreach (var requirement in set.Requirements)
        {
            var testable = requirement.Testable ? $"{requirement.TestCount} cases" : $"not testable: {requirement.Reason}";
            text.AppendLine($"{requirement.Id}: {requirement.Text} ({testable})");
        }

        text.AppendLine().AppendLine("Cases:");
        foreach (var testCase in set.Cases)
        {
            text.AppendLine($"{testCase.Id} {testCase.Title} [{testCase.Category}; {string.Join(", ", testCase.RequirementIds)}]");
            text.AppendLine($"  Steps: {OneLine(testCase.Steps)}");
            text.AppendLine($"  Expected: {(testCase.Expected is { } expected ? OneLine(expected) : "from the reference solution")}");
        }

        if (set.Style == TestStyle.ClassDesign)
        {
            if (set.Interface is { } source)
            {
                text.AppendLine().AppendLine("Interface the tests call:").AppendLine(Shorten(source.TrimEnd(), MaxInterfaceLength));
            }

            text.AppendLine().Append(set.Code switch
            {
                null => "Test code: not written yet (it is written after the teacher approves the cases).",
                { Status: TestCodeStatus.CompileFailed } => "Test code: does not compile; the teacher writes it again on the Tests view.",
                _ => "Test code: ready, so the tests can run on students.",
            });
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd();
    }

    [AgentTool("run_tests")]
    [Description(
        "Runs the approved class-design tests on every student's latest version of an assignment, in the Docker sandbox. Each "
        + "student needs an adapter that connects their classes to the test interface: a stored one is reused for free, a missing "
        + "one is written with one small paid AI call. It returns one job id per student at once; wait for them, then read the "
        + "results with get_test_results. Gradings run the tests on their own, so use this only when the teacher asks to run "
        + "the tests.")]
    public async Task<string> RunTestsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (await _testSets.GetLatestApprovedAsync(assignmentId, cancellationToken) is not { } set)
        {
            return NoApprovedSet;
        }

        var students = await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken);
        var jobIds = new List<Guid>();
        var noWork = 0;
        try
        {
            foreach (var submission in students)
            {
                if (submission.LatestVersion is not { Status: not VersionStatus.Failed } version)
                {
                    noWork++;
                    continue;
                }

                if (await _runs.QueueOneAsync(assignmentId, set.Id, version.Id, cancellationToken: cancellationToken) is { } job)
                {
                    jobIds.Add(job.Id);
                }
            }
        }
        catch (InvalidRequestException ex)
        {
            return $"Not started: {ex.Message}";
        }

        if (jobIds.Count == 0)
        {
            return $"Nothing to run: none of the {students.Count} students has stored work. Fetch the submissions first.";
        }

        _context.RecordLinks([Link(courseId, assignmentId)]);
        var skipped = noWork > 0 ? $" {noWork} students have no stored work and were left out." : "";
        return $"Test runs of version {set.Version} queued for {jobIds.Count} students (job ids {string.Join(", ", jobIds)}).{skipped} "
            + "Each takes under a minute; wait for them before reading the results.";
    }

    [AgentTool("run_tests_for_student")]
    [Description(
        "Runs the approved class-design tests on one student's latest version of an assignment, in the Docker sandbox, reusing "
        + "their stored adapter (free) or writing one when there is none (one small paid AI call). Set newAdapter when the "
        + "results show the adapter connected the student's classes wrongly (a harness failure, or cases failing because a method "
        + "was mapped to the wrong one). It returns a job id at once; wait for it, then read the results with get_test_results.")]
    public async Task<string> RunTestsForStudentAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("The student id, as returned by find_students")] string studentId,
        [Description("true writes the student's adapter again (one small paid AI call) instead of reusing the stored one; set it "
            + "only when the teacher asks, or agrees after you explain why the adapter looks wrong")] bool newAdapter = false,
        CancellationToken cancellationToken = default)
    {
        if (await _testSets.GetLatestApprovedAsync(assignmentId, cancellationToken) is not { } set)
        {
            return NoApprovedSet;
        }

        var submission = (await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .FirstOrDefault(s => s.StudentId == studentId);
        if (submission?.LatestVersion is not { Status: not VersionStatus.Failed } version)
        {
            return "Not started: this student has no stored work for this assignment. Fetch the submissions first.";
        }

        try
        {
            var job = await _runs.QueueOneAsync(assignmentId, set.Id, version.Id, newAdapter, cancellationToken);
            if (job is null)
            {
                return "Not started: the test set or the student's version no longer exists.";
            }

            _context.RecordQueuedJob(job.Id);
            _context.RecordLinks([Link(courseId, assignmentId)]);
            var adapter = newAdapter ? "with a newly written adapter" : "reusing the stored adapter if there is one";
            return $"Test run of version {set.Version} queued for the student's version {version.Number}, {adapter} (job id {job.Id}).";
        }
        catch (InvalidRequestException ex)
        {
            return $"Not started: {ex.Message}";
        }
    }

    [AgentTool("get_test_results")]
    [Description(
        "Reads the latest test results of the approved test set for an assignment: per student (by student id, no names; find_students "
        + "gives the ids of named students) the run status, how many cases passed, and every case that did not pass with its "
        + "message. It is free. Give a student id to read only that student.")]
    public async Task<string> GetTestResultsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("Only this student's results; leave out for the whole class")] string? studentId = null,
        CancellationToken cancellationToken = default)
    {
        if (await _testSets.GetLatestApprovedAsync(assignmentId, cancellationToken) is not { } set)
        {
            return NoApprovedSet;
        }

        var runs = (await _runs.GetLatestAsync(assignmentId, set.Id, cancellationToken) ?? [])
            .Where(r => studentId is null || r.StudentId == studentId)
            .GroupBy(r => r.StudentId)
            .Select(g => g.MaxBy(r => r.VersionNumber)!)
            .OrderBy(r => r.StudentId, StringComparer.Ordinal)
            .ToList();
        if (runs.Count == 0)
        {
            return studentId is null
                ? $"No test results for version {set.Version} yet. run_tests runs them; a grading also runs them for its student."
                : $"No test results for this student with version {set.Version} yet. run_tests_for_student runs them.";
        }

        _context.RecordLinks([Link(courseId, assignmentId)]);
        var titles = TestDesignService.ReadContent(set).Cases.ToDictionary(c => c.Id, c => c.Title);
        var text = new StringBuilder();
        text.AppendLine($"Results of test set version {set.Version} ({titles.Count} cases):");
        foreach (var run in runs)
        {
            text.AppendLine().Append($"Student {run.StudentId}, version {run.VersionNumber}: ");
            switch (run.Status)
            {
                case TestRunStatus.Completed:
                    text.AppendLine($"{run.Passed} of {run.Results.Count} passed.");
                    foreach (var result in run.Results.Where(r => r.Outcome != TestOutcome.Passed))
                    {
                        var outcome = result.Outcome switch
                        {
                            TestOutcome.Failed => "failed",
                            TestOutcome.NotImplemented => "not implemented",
                            _ => "did not run",
                        };
                        var message = result.Message is { } m ? $": {Shorten(OneLine(m), MaxMessageLength)}" : "";
                        text.AppendLine($"  {result.CaseId} {titles.GetValueOrDefault(result.CaseId, "")} {outcome}{message}");
                    }

                    if (run.Unmapped.Count > 0)
                    {
                        text.AppendLine($"  The adapter found nothing for: {string.Join(", ", run.Unmapped)}.");
                    }

                    break;
                case TestRunStatus.StudentCompileFailed:
                    text.AppendLine($"not run, the student's code does not compile. {Shorten(OneLine(run.Output ?? ""), MaxMessageLength)}");
                    break;
                case TestRunStatus.HarnessFailed:
                    text.AppendLine($"the adapter did not fit this submission, so the results are not usable. {Shorten(OneLine(run.Output ?? ""), MaxMessageLength)}");
                    break;
                default:
                    text.AppendLine($"not run. {Shorten(OneLine(run.Output ?? ""), MaxMessageLength)}");
                    break;
            }
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd();
    }

    private static AgentLink Link(string courseId, string assignmentId)
    {
        return new AgentLink
        {
            Kind = AgentLinkKind.Tests,
            Label = "The assignment's tests",
            CourseId = courseId,
            AssignmentId = assignmentId,
        };
    }

    private static bool TryParse<T>(string text, out T value)
        where T : struct, Enum
    {
        return Enum.TryParse(text.Trim(), ignoreCase: true, out value) && Enum.IsDefined(value);
    }

    private static string OneLine(string text)
    {
        return string.Join("; ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string Shorten(string text, int max)
    {
        return text.Length <= max ? text : text[..max] + "…";
    }
}
