using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

public class AgentRunWakerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAgentChats _chats = new();
    private readonly FakeAgentJobs _jobs = new();
    private readonly Clock _clock = new(Now);

    private AgentRunWaker Waker()
    {
        return new AgentRunWaker(_chats, _jobs, _jobs, new JobReporter(_jobs, _jobs, _jobs), _clock);
    }

    private AgentRun WaitingRun(params Job[] jobs)
    {
        var conversation = new AgentConversation(Now);
        var run = new AgentRun(conversation.Id, Now);
        _chats.Conversations.Add(conversation);
        _chats.Messages.Add(conversation.AddUserMessage(run.Id, "grade and wait", Now));
        _chats.Runs.Add(run);
        run.Start(Now);
        run.WaitFor(jobs.Select(j => j.Id).ToList(), Now + AgentRun.WaitLimit);
        return run;
    }

    private Job Grading(string student, double? score)
    {
        var versionId = Guid.NewGuid();
        var job = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload(versionId)), Now);
        _jobs.Jobs.Add(job);
        _jobs.Subjects.Add(new JobSubject(versionId, 2, "course-1", "a-4", "Assignment 4", $"sub-{student}", "s-1", student));
        if (score is { } points)
        {
            var grading = new AiGrading(versionId, "model", "review", points, 100, null, 1, [], [], "{}", [], Now);
            _jobs.Gradings.Add(grading);
            Finish(job, JobPayloads.Write(new GradeVersionResult(grading.Id)));
        }

        return job;
    }

    private static void Finish(Job job, string? result)
    {
        job.Start("worker", Now, TimeSpan.FromMinutes(10));
        job.Succeed(result, Now);
    }

    [Fact]
    public async Task ARun_IsWokenOnlyWhenEveryJobItWaitsForHasFinished()
    {
        var first = Grading("Nino", 78);
        var second = Grading("Giorgi", null);
        var run = WaitingRun(first, second);

        await Waker().OnJobFinishedAsync(first.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AgentRunStatus.Waiting, run.Status);

        var grading = new AiGrading(Guid.NewGuid(), "model", "review", 64, 100, null, 1, [], [], "{}", [], Now);
        _jobs.Gradings.Add(grading);
        Finish(second, JobPayloads.Write(new GradeVersionResult(grading.Id)));
        await Waker().OnJobFinishedAsync(second.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        var notice = _chats.Messages[^1];
        Assert.Equal(AgentMessageRole.Notice, notice.Role);
        Assert.Contains("The background work you waited for has finished:", notice.Text);
        Assert.Contains($"- Grading of Nino's Assignment 4, version 2: draft ready, score 78 of 100 (job id {first.Id})", notice.Text);
        Assert.Contains("- Grading of Giorgi's Assignment 4, version 2: draft ready, score 64 of 100", notice.Text);

        Assert.Equal(
        [
            new AgentLink(AgentLinkKind.Submission, "Nino's draft · Assignment 4", "course-1", "a-4", "sub-Nino"),
        ], AgentLinks.Read(notice.Links).Where(l => l.Label.StartsWith("Nino")));
        Assert.Equal(2, AgentLinks.Read(notice.Links).Count);

        var resumed = _jobs.Jobs.Single(j => j.Type == JobType.AgentRun);
        Assert.Equal(run.Id, JobPayloads.Read<AgentRunPayload>(resumed.Payload).RunId);
        Assert.Equal(resumed.Id, run.JobId);
        Assert.Equal(1, _jobs.Notified);
    }

    [Fact]
    public async Task TheNotice_CarriesTheCommonMistakesAnalysis()
    {
        var analysis = new MistakeAnalysis("course-1", "a-4", "model", "Most students mixed up composition and inheritance.",
            "Revisit composition with one worked example.", 15, 40,
        [
            new MistakeCluster("Inheritance used for code reuse", "…", false, [Member("s-1"), Member("s-2"), Member("s-2")]),
            new MistakeCluster("Public fields", "…", false, [Member("s-3")]),
            new MistakeCluster("Other", "…", true, [Member("s-4"), Member("s-5")]),
        ], Now);
        _jobs.Analyses.Add(analysis);
        var job = new Job(JobType.AnalyzeMistakes, JobPayloads.Write(new AnalyzeMistakesPayload("course-1", "a-4")), Now,
            courseId: "course-1", assignmentId: "a-4");
        _jobs.Jobs.Add(job);
        Finish(job, JobPayloads.Write(new AnalyzeMistakesResult(analysis.Id)));
        WaitingRun(job);

        await Waker().OnJobFinishedAsync(job.Id, TestContext.Current.CancellationToken);

        var notice = _chats.Messages[^1].Text;
        Assert.Contains("Common mistakes analysis ready: 40 mistakes from 15 students.", notice);
        Assert.Contains("Overview: Most students mixed up composition and inheritance.", notice);
        Assert.Contains("  - Inheritance used for code reuse: 2 students, 3 mistakes\n  - Public fields: 1 student, 1 mistake", notice);
        Assert.Contains("(2 mistakes not grouped)", notice);
        Assert.Contains("For the next lecture: Revisit composition with one worked example.", notice);
        Assert.Equal([new AgentLink(AgentLinkKind.CommonMistakes, "Common mistakes", "course-1", "a-4")], AgentLinks.Read(_chats.Messages[^1].Links));
    }

    private static ClusterMember Member(string studentId)
    {
        return new ClusterMember(Guid.NewGuid(), Guid.NewGuid(), studentId, "Name", "sub", MistakeSeverity.Minor, MistakeCategory.Naming,
            1, "message", null, null, null);
    }

    [Fact]
    public async Task ARun_IsWokenWithTheProgress_WhenItsWaitIsOver()
    {
        var grading = Grading("Nino", null);
        var run = WaitingRun(grading);

        Assert.Equal(0, await Waker().SweepAsync(TestContext.Current.CancellationToken));

        _clock.Now = Now + AgentRun.WaitLimit;
        Assert.Equal(1, await Waker().SweepAsync(TestContext.Current.CancellationToken));

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        Assert.Contains("Not everything has finished after 30 minutes:", _chats.Messages[^1].Text);
        Assert.Contains("Grading of Nino's Assignment 4, version 2: still queued", _chats.Messages[^1].Text);
        Assert.Null(_chats.Messages[^1].Links); // nothing finished, so nothing to open yet
    }

    [Fact]
    public async Task ARun_IsWokenAsSoonAsItsOwnTurnEnds_WhenItsJobFinishedBeforeItStartedWaiting()
    {
        var analysis = new Job(JobType.AnalyzeMistakes, JobPayloads.Write(new AnalyzeMistakesPayload("course-1", "a-4")), Now,
            courseId: "course-1", assignmentId: "a-4");
        _jobs.Jobs.Add(analysis);
        analysis.Start("worker", Now, TimeSpan.FromMinutes(10));
        analysis.Fail("No student of this assignment has an AI draft yet.", temporary: false, Now);

        var conversation = new AgentConversation(Now);
        var run = new AgentRun(conversation.Id, Now);
        _chats.Conversations.Add(conversation);
        _chats.Runs.Add(run);
        var turn = new Job(JobType.AgentRun, JobPayloads.Write(new AgentRunPayload(run.Id)), Now, maxAttempts: 1);
        _jobs.Jobs.Add(turn);
        run.AttachJob(turn.Id);
        run.Start(Now);
        run.WaitFor([analysis.Id], Now + AgentRun.WaitLimit);
        Finish(turn, null);

        await Waker().OnJobFinishedAsync(analysis.Id, TestContext.Current.CancellationToken); // nothing: the analysis finished earlier
        await Waker().OnJobFinishedAsync(turn.Id, TestContext.Current.CancellationToken); // the turn ending wakes it

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        Assert.Contains("Common mistakes analysis failed: No student of this assignment has an AI draft yet.", _chats.Messages[^1].Text);
    }

    [Fact]
    public async Task TheSweep_WakesARunWhoseJobsFinishedUnnoticed()
    {
        var grading = Grading("Nino", 78);
        var run = WaitingRun(grading);

        await Waker().SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AgentRunStatus.Queued, run.Status);
    }

    [Fact]
    public async Task ARunWhoseOwnJobFailed_IsMarkedFailed_SoItsConversationIsNotBlocked()
    {
        var conversation = new AgentConversation(Now);
        var run = new AgentRun(conversation.Id, Now);
        _chats.Runs.Add(run);
        var job = new Job(JobType.AgentRun, JobPayloads.Write(new AgentRunPayload(run.Id)), Now, maxAttempts: 1);
        _jobs.Jobs.Add(job);
        run.AttachJob(job.Id);
        run.Start(Now);
        job.Start("worker", Now, TimeSpan.FromMinutes(10));
        job.RecoverExpiredLease(Now.AddMinutes(11));

        await Waker().SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.StartsWith("The worker stopped while running this job", run.Error);
    }

    [Fact]
    public async Task AQueuedRunWhoseJobWasCanceled_IsCanceled()
    {
        var conversation = new AgentConversation(Now);
        var run = new AgentRun(conversation.Id, Now);
        _chats.Runs.Add(run);
        var job = new Job(JobType.AgentRun, JobPayloads.Write(new AgentRunPayload(run.Id)), Now, maxAttempts: 1);
        _jobs.Jobs.Add(job);
        run.AttachJob(job.Id);
        job.Cancel(Now);

        await Waker().OnJobFinishedAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AgentRunStatus.Canceled, run.Status);
    }

    [Fact]
    public async Task WaitTool_ReturnsTheResultsAtOnce_WhenTheJobsHaveAlreadyFinished()
    {
        var grading = Grading("Nino", 78);
        var context = new AgentRunContext();
        context.Begin(Guid.NewGuid());
        var tools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs), context);

        var result = await tools.WaitForJobsAsync([grading.Id.ToString()], TestContext.Current.CancellationToken);

        Assert.StartsWith("All of these jobs have already finished:", result);
        Assert.Contains("score 78 of 100", result);
        Assert.False(context.StopRequested);
    }

    [Fact]
    public async Task WaitTool_AsksToStop_ForJobsStillInProgress()
    {
        var done = Grading("Nino", 78);
        var running = Grading("Giorgi", null);
        var context = new AgentRunContext();
        context.Begin(Guid.NewGuid());
        var tools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs), context);

        var result = await tools.WaitForJobsAsync([done.Id.ToString(), running.Id.ToString()], TestContext.Current.CancellationToken);

        Assert.StartsWith("Waiting for 1 job still in progress.", result);
        Assert.Equal([running.Id], context.WaitFor);
        Assert.True(context.StopRequested);
    }

    [Fact]
    public async Task WaitTool_RejectsAnUnknownId_AndWorksOnlyInsideARun()
    {
        var context = new AgentRunContext();
        var tools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs), context);

        Assert.StartsWith("Waiting is not possible here", await tools.WaitForJobsAsync(["x"], TestContext.Current.CancellationToken));

        context.Begin(Guid.NewGuid());
        Assert.Equal(
            "No background job has the id 'x'. Use the job ids from the tools' answers.",
            await tools.WaitForJobsAsync(["x"], TestContext.Current.CancellationToken));
        Assert.False(context.StopRequested);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
