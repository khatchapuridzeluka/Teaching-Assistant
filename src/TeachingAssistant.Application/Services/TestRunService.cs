using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Application.Services;

public class TestRunService : ITestRunService
{
    private readonly ITestSetStore _testSets;
    private readonly ITestRunStore _runs;
    private readonly ISubmissionStore _submissions;
    private readonly TestRunner _runner;
    private readonly IJobService _jobs;

    public TestRunService(ITestSetStore testSets, ITestRunStore runs, ISubmissionStore submissions, TestRunner runner, IJobService jobs)
    {
        _testSets = testSets;
        _runs = runs;
        _submissions = submissions;
        _runner = runner;
        _jobs = jobs;
    }

    public async Task<RunTestsResponse?> QueueAllAsync(string assignmentId, Guid testSetId, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken);
        if (testSet is null || testSet.AssignmentId != assignmentId)
        {
            return null;
        }

        TestRunner.EnsureCanRun(testSet, TestDesignService.ReadContent(testSet));
        var queued = 0;
        var skipped = new List<string>();
        foreach (var submission in await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
        {
            if (submission.LatestVersion is not { Status: not VersionStatus.Failed } version)
            {
                skipped.Add($"{submission.StudentName}: no stored work to test");
                continue;
            }

            await _jobs.QueueRunTestsAsync(testSet.CourseId, assignmentId, testSet.Id, version.Id, cancellationToken: cancellationToken);
            queued++;
        }

        return new RunTestsResponse { Queued = queued, Skipped = skipped };
    }

    public async Task<JobResponse?> QueueOneAsync(
        string assignmentId, Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken);
        var submission = await _submissions.FindByVersionAsync(versionId, cancellationToken);
        if (testSet is null || testSet.AssignmentId != assignmentId || submission is null || submission.AssignmentId != assignmentId)
        {
            return null;
        }

        TestRunner.EnsureCanRun(testSet, TestDesignService.ReadContent(testSet));
        return await _jobs.QueueRunTestsAsync(testSet.CourseId, assignmentId, testSet.Id, versionId, newAdapter, cancellationToken);
    }

    public async Task<IReadOnlyList<TestRunResponse>?> GetLatestAsync(
        string assignmentId, Guid testSetId, CancellationToken cancellationToken = default)
    {
        var testSet = await _testSets.FindAsync(testSetId, cancellationToken);
        if (testSet is null || testSet.AssignmentId != assignmentId)
        {
            return null;
        }

        var versions = (await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .SelectMany(s => s.Versions.Select(v => (Submission: s, Version: v)))
            .ToDictionary(x => x.Version.Id);
        var cases = TestDesignService.ReadContent(testSet).Cases;
        return (await _runs.GetLatestByTestSetAsync(testSetId, cancellationToken))
            .Where(r => versions.ContainsKey(r.VersionId))
            .Select(r => ToResponse(r, versions[r.VersionId].Submission, versions[r.VersionId].Version, cases))
            .OrderBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task RunAsync(Guid testSetId, Guid versionId, bool newAdapter = false, CancellationToken cancellationToken = default)
    {
        await _runner.RunAsync(testSetId, versionId, newAdapter, cancellationToken);
    }

    private static TestRunResponse ToResponse(TestRun run, Submission submission, SubmissionVersion version, IReadOnlyList<TestCase> cases)
    {
        var details = TestRunner.ReadDetails(run);
        var order = cases.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var results = details.Results.OrderBy(r => order.GetValueOrDefault(r.CaseId, int.MaxValue)).ToList();
        return new TestRunResponse
        {
            Id = run.Id,
            VersionId = run.VersionId,
            VersionNumber = version.Number,
            StudentId = submission.StudentId,
            StudentName = submission.StudentName,
            TestSetVersion = run.TestSetVersion,
            Status = run.Status,
            Results = results,
            Passed = results.Count(r => r.Outcome == TestOutcome.Passed),
            Failed = results.Count(r => r.Outcome == TestOutcome.Failed),
            NotImplemented = results.Count(r => r.Outcome == TestOutcome.NotImplemented),
            Adapter = run.Adapter,
            Unmapped = details.Unmapped,
            Notes = details.Notes,
            Output = run.Output,
            CreatedAt = run.CreatedAt,
        };
    }
}
