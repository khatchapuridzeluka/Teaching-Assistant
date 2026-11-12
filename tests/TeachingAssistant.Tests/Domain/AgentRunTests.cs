using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Tests.Domain;

public class AgentRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static AgentRun Run(int maxSteps = AgentRun.DefaultMaxSteps)
    {
        return new AgentRun(Guid.NewGuid(), Now, "Assignment 'A4' in course 'Java'", "course-1", "assignment-4", maxSteps);
    }

    [Fact]
    public void ARun_WaitsForJobsUntilADeadline_AndIsQueuedAgainWhenResumed()
    {
        var run = Run();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        run.AttachJob(Guid.NewGuid());

        run.Start(Now);
        run.WaitFor([first, second, first], Now.AddMinutes(30));
        Assert.Equal(AgentRunStatus.Waiting, run.Status);
        Assert.Equal([first, second], run.WaitingJobIds);
        Assert.False(run.IsWaitOver(Now.AddMinutes(29)));
        Assert.True(run.IsWaitOver(Now.AddMinutes(30)));

        run.Resume();
        Assert.Equal((AgentRunStatus.Queued, (Guid?)null, (DateTimeOffset?)null, (Guid?)null), (run.Status, run.JobId, run.WaitUntil, run.JobId));
        Assert.Empty(run.WaitingJobIds);

        run.Start(Now.AddMinutes(5));
        Assert.Equal(Now, run.StartedAt); // the first start is kept
    }

    [Fact]
    public void ARun_MustWaitForAtLeastOneJob_AndOnlyWhileRunning()
    {
        var run = Run();

        Assert.Throws<InvalidOperationException>(() => run.WaitFor([Guid.NewGuid()], Now)); // not started
        Assert.Throws<InvalidOperationException>(() => run.Resume()); // not waiting

        run.Start(Now);
        Assert.Throws<ArgumentException>(() => run.WaitFor([], Now));
        Assert.Throws<InvalidOperationException>(() => run.AttachJob(Guid.NewGuid())); // only a queued run gets a job
    }

    [Fact]
    public void ARun_StopsTakingStepsAtItsLimit()
    {
        var run = Run(maxSteps: 2);
        run.Start(Now);

        Assert.True(run.TryTakeStep());
        Assert.True(run.TryTakeStep());
        Assert.False(run.TryTakeStep());
        Assert.Equal(2, run.Steps);
    }

    [Fact]
    public void ARun_SucceedsWithItsReply()
    {
        var run = Run();
        run.Start(Now);

        run.Succeed("  The draft is ready.  ", Now.AddMinutes(1));

        Assert.Equal((AgentRunStatus.Succeeded, "The draft is ready.", (DateTimeOffset?)Now.AddMinutes(1)), (run.Status, run.Reply, run.FinishedAt));
        Assert.True(run.IsFinished);
    }

    [Fact]
    public void ARun_CanFailOrBeCanceledWhileWaiting_AndStopsWaiting()
    {
        var failed = Run();
        failed.Start(Now);
        failed.WaitFor([Guid.NewGuid()], Now.AddMinutes(30));
        failed.Fail("The model is unreachable.", Now);
        Assert.Equal((AgentRunStatus.Failed, "The model is unreachable."), (failed.Status, failed.Error));
        Assert.Empty(failed.WaitingJobIds);
        Assert.Null(failed.WaitUntil);

        var canceled = Run();
        canceled.Cancel(Now);
        Assert.Equal(AgentRunStatus.Canceled, canceled.Status);
    }

    [Fact]
    public void ARun_RejectsStepsThatDoNotFitItsState()
    {
        var run = Run();

        Assert.Throws<InvalidOperationException>(() => run.TryTakeStep()); // not started
        Assert.Throws<InvalidOperationException>(() => run.Succeed("done", Now));

        run.Start(Now);
        run.Succeed("done", Now);

        Assert.Throws<InvalidOperationException>(() => run.Start(Now));
        Assert.Throws<InvalidOperationException>(() => run.Fail("late", Now));
        Assert.Throws<InvalidOperationException>(() => run.Cancel(Now));
        Assert.Throws<InvalidOperationException>(() => run.Resume());
    }

    [Fact]
    public void AConversation_NumbersItsMessagesInOrder_AndTracksItsLastActivity()
    {
        var conversation = new AgentConversation(Now);
        var run = Guid.NewGuid();
        var job = Guid.NewGuid();

        var question = conversation.AddUserMessage(run, " fetch assignment 4 ", Now.AddSeconds(1));
        var call = conversation.AddAssistantMessage(run, "", """[{"id":"call-1","name":"fetch_submissions"}]""", Now.AddSeconds(2));
        var result = conversation.AddToolResult(run, "call-1", "fetch_submissions", "Fetch queued.", job, Now.AddSeconds(3));
        var notice = conversation.AddNotice(run, "The fetch finished: 5 new submissions.", job, Now.AddSeconds(4));

        Assert.Equal([0, 1, 2, 3], new[] { question, call, result, notice }.Select(m => m.Sequence));
        Assert.Equal(
            [AgentMessageRole.User, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Notice],
            new[] { question, call, result, notice }.Select(m => m.Role));
        Assert.Equal("fetch assignment 4", question.Text);
        Assert.Equal((job, "call-1", "fetch_submissions"), (result.JobId, result.ToolCallId, result.ToolName));
        Assert.Equal((4, Now.AddSeconds(4)), (conversation.MessageCount, conversation.LastActivityAt));
    }

    [Fact]
    public void AToolResult_NeedsTheCallItAnswers()
    {
        var conversation = new AgentConversation(Now);

        Assert.Throws<ArgumentException>(() => conversation.AddToolResult(Guid.NewGuid(), "", "fetch_submissions", "done", null, Now));
        Assert.Equal(0, conversation.MessageCount); // a rejected message takes no number
    }
}
