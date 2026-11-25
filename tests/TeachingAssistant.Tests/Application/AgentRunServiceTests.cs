using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

public class AgentRunServiceTests
{
    private readonly FakeAgentChats _chats = new();
    private readonly FakeAgentJobs _jobs = new();
    private readonly ScriptedModel _model = new();
    private readonly AgentRunContext _runContext = new();
    private readonly ClassroomTools _tools = new(null!, null!, null!, null!, null!); // never called here: only the model calls tools
    private readonly StudentTools _studentTools = new(null!, null!, null!, null!, null!, null!, TimeProvider.System);

    private AgentRunService Service()
    {
        var agentTools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs, _jobs), _runContext);
        var gradingTools = new GradingTools(null!, null!, null!, _runContext, TimeProvider.System);
        var analysisTools = new StudentAnalysisTools(null!, null!, _runContext, TimeProvider.System);
        return new AgentRunService(_chats, _jobs, _jobs, _model, _tools, _studentTools, agentTools, gradingTools, analysisTools, _runContext,
            new ModelCallContext(), TimeProvider.System);
    }

    private async Task<AgentRunResponse> StartAsync(string text, Guid? conversationId = null)
    {
        var run = await Service().StartAsync(
            new StartAgentRunRequest
            {
                Text = text,
                ConversationId = conversationId,
                Page = "Assignment 'A4' in course 'Java'",
                CourseId = "course-1",
                AssignmentId = "assignment-4",
            },
            TestContext.Current.CancellationToken);
        return run!;
    }

    [Fact]
    public async Task Start_StoresTheMessageInANewConversation_AndQueuesTheRun()
    {
        var run = await StartAsync("  fetch assignment 4  ");

        Assert.Equal(AgentRunStatus.Queued, run.Status);
        var message = Assert.Single(run.Messages);
        Assert.Equal((AgentMessageRole.User, "fetch assignment 4", 0), (message.Role, message.Text, message.Sequence));
        Assert.Equal(run.ConversationId, Assert.Single(_chats.Conversations).Id);

        var job = Assert.Single(_jobs.Jobs);
        Assert.Equal((JobType.AgentRun, 1, "course-1", "assignment-4"), (job.Type, job.MaxAttempts, job.CourseId, job.AssignmentId));
        Assert.Equal(run.Id, JobPayloads.Read<AgentRunPayload>(job.Payload).RunId);
        Assert.Equal(job.Id, _chats.Runs.Single().JobId);
        Assert.Equal(1, _jobs.Notified);
    }

    [Fact]
    public async Task Start_RejectsAnEmptyMessage_AndAnUnknownConversation()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => StartAsync("   "));

        var unknown = await Service().StartAsync(new StartAgentRunRequest { Text = "hi", ConversationId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        Assert.Null(unknown);
        Assert.Empty(_jobs.Jobs);
    }

    [Fact]
    public async Task Start_RejectsANewMessage_WhileThePreviousRunIsUnfinished()
    {
        var first = await StartAsync("fetch assignment 4");

        await Assert.ThrowsAsync<InvalidRequestException>(() => StartAsync("and grade it", first.ConversationId));
    }

    [Fact]
    public async Task Execute_StoresEveryStepInOrder_AndSucceedsWithTheFinalText()
    {
        var started = await StartAsync("what courses do I have?");
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "list_courses",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry
                {
                    Role = AgentMessageRole.Tool,
                    Text = "- Java II (course id course-1, ACTIVE)",
                    ToolCallId = "call-1",
                    ToolName = "list_courses",
                },
                new AgentEntry { Role = AgentMessageRole.Assistant, Text = "You have one course: Java II." },
            ],
        };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Succeeded, "You have one course: Java II.", 2), (run.Status, run.Reply, run.Steps));
        Assert.Equal(
            [AgentMessageRole.User, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant],
            run.Messages.Select(m => m.Role));
        Assert.Equal([0, 1, 2, 3], run.Messages.Select(m => m.Sequence));
        Assert.Equal(["list_courses"], run.Messages[1].ToolCalls);
        Assert.Equal("list_courses", run.Messages[2].ToolName);

        Assert.Equal("what courses do I have?", Assert.Single(_model.History!).Text);
        Assert.Contains("(course id course-1, assignment id assignment-4)", _model.System);
        Assert.Equal(5, _model.Tools!.Count);
        Assert.Same(_tools, _model.Tools[0]);
        Assert.Same(_studentTools, _model.Tools[1]);
    }

    [Fact]
    public async Task SuggestedReplies_AreSavedWithTheirToolStep_TrimmedDistinctAndAtMostThree()
    {
        var started = await StartAsync("make a pdf of Sandro's assignment 2");
        var tools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs, _jobs), _runContext);
        string? answer = null;
        _model.OnTool = entry =>
        {
            if (entry.ToolName == "suggest_replies")
            {
                answer = tools.SuggestReplies([" Yes, grade it ", "yes, grade it", "", new string('x', 61), "No, leave it", "Grade all his work", "Later"]);
            }
        };
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "He has no grading yet. Should I grade it first?",
                    ToolCalls = [new AgentToolCall { Id = "call-1", Name = "suggest_replies", Arguments = "{}" }],
                },
                new AgentEntry
                {
                    Role = AgentMessageRole.Tool,
                    Text = "3 buttons are shown",
                    ToolCallId = "call-1",
                    ToolName = "suggest_replies",
                },
                new AgentEntry { Role = AgentMessageRole.Assistant, Text = "" },
            ],
        };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Succeeded, "He has no grading yet. Should I grade it first?"), (run.Status, run.Reply));
        Assert.Equal(["Yes, grade it", "No, leave it", "Grade all his work"], run.Messages[2].Replies);
        Assert.Empty(run.Messages[3].Replies);
        Assert.StartsWith("3 buttons are shown under your question.", answer);
    }

    [Fact]
    public void SuggestReplies_WithNoUsableAnswer_ShowsNothing()
    {
        var tools = new AgentTools(_jobs, new JobReporter(_jobs, _jobs, _jobs, _jobs), _runContext);

        var answer = tools.SuggestReplies(["  ", new string('x', 61)]);

        Assert.StartsWith("No buttons shown", answer);
        Assert.Empty(_runContext.TakeReplies());
    }

    [Fact]
    public async Task Execute_OfAFollowUp_SendsTheEarlierToolStepsBackToTheModel()
    {
        var first = await StartAsync("what courses do I have?");
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "list_courses",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry { Role = AgentMessageRole.Tool, Text = "- Java II", ToolCallId = "call-1", ToolName = "list_courses" },
                new AgentEntry { Role = AgentMessageRole.Assistant, Text = "Java II." },
            ],
        };
        await Service().ExecuteAsync(first.Id, TestContext.Current.CancellationToken);

        var second = await StartAsync("and its assignments?", first.ConversationId);
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [new AgentEntry { Role = AgentMessageRole.Assistant, Text = "Assignment 4." }],
        };
        await Service().ExecuteAsync(second.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            [AgentMessageRole.User, AgentMessageRole.Assistant, AgentMessageRole.Tool, AgentMessageRole.Assistant, AgentMessageRole.User],
            _model.History!.Select(e => e.Role));
        var call = Assert.Single(_model.History![1].ToolCalls!);
        Assert.Equal(("call-1", "list_courses", "{}"), (call.Id, call.Name, call.Arguments));
        Assert.Equal("call-1", _model.History[2].ToolCallId);

        var conversation = (await Service().GetConversationAsync(first.ConversationId, TestContext.Current.CancellationToken))!;
        Assert.Equal(6, conversation.Messages.Count);
        Assert.Null(conversation.ActiveRunId);
    }

    [Fact]
    public async Task Execute_MarksTheRunFailed_WhenTheModelFails()
    {
        var started = await StartAsync("hi");
        _model.Failure = new LanguageModelUnavailableException("Rate limited by the Anthropic API. Try again shortly.");

        await Assert.ThrowsAsync<LanguageModelUnavailableException>(() =>
            Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken));

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Failed, "Rate limited by the Anthropic API. Try again shortly."), (run.Status, run.Error));
    }

    [Fact]
    public async Task Execute_FailsTheRun_WhenTheModelStopsWithoutAnAnswer()
    {
        var started = await StartAsync("hi");
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "list_courses",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry { Role = AgentMessageRole.Tool, Text = "- Java II", ToolCallId = "call-1", ToolName = "list_courses" },
            ],
        };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal((AgentRunStatus.Failed, "The assistant stopped without an answer."), (run.Status, run.Error));
        Assert.Equal(3, run.Messages.Count); // the steps it did take are kept
    }

    [Fact]
    public async Task Execute_SavesEachStepAsItHappens()
    {
        var started = await StartAsync("what courses do I have?");
        var savesBefore = _chats.Saves;
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "list_courses",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry { Role = AgentMessageRole.Tool, Text = "- Java II", ToolCallId = "call-1", ToolName = "list_courses" },
                new AgentEntry { Role = AgentMessageRole.Assistant, Text = "Java II." },
            ],
        };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        Assert.True(_chats.Saves - savesBefore >= 4); // the start, one save per step, and the end
    }

    [Fact]
    public async Task Execute_EndsTheTurnAndWaits_WhenTheModelWaitsForAJob()
    {
        var started = await StartAsync("grade Nino and tell me the score");
        var grading = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload { VersionId = Guid.NewGuid() }), DateTimeOffset.UtcNow);
        _jobs.Jobs.Add(grading);
        _model.OnTool = entry =>
        {
            if (entry.ToolName == "grade_student_assignment")
            {
                _runContext.RecordQueuedJob(grading.Id);
                _runContext.RecordLinks([new AgentLink
                {
                    Kind = AgentLinkKind.Submission,
                    Label = "Nino's submission",
                    CourseId = "course-1",
                    AssignmentId = "a-4",
                    SubmissionId = "sub-1",
                }]);
            }
            else if (entry.ToolName == "wait_for_jobs")
            {
                _runContext.RequestWait([grading.Id]);
            }
        };
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "grade_student_assignment",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry
                {
                    Role = AgentMessageRole.Tool,
                    Text = $"Grading queued (job id {grading.Id})",
                    ToolCallId = "call-1",
                    ToolName = "grade_student_assignment",
                },
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-2",
                        Name = "wait_for_jobs",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry
                {
                    Role = AgentMessageRole.Tool,
                    Text = "Waiting for 1 job",
                    ToolCallId = "call-2",
                    ToolName = "wait_for_jobs",
                },
                new AgentEntry { Role = AgentMessageRole.Assistant, Text = "This must not be reached." },
            ],
        };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(AgentRunStatus.Waiting, run.Status);
        Assert.Equal([grading.Id], run.WaitingJobIds);
        Assert.NotNull(run.WaitUntil);
        Assert.Equal(5, run.Messages.Count); // the turn stopped after the wait
        Assert.Equal(grading.Id, run.Messages[2].JobId); // the grading's result links to its job
        Assert.Equal("Nino's submission", Assert.Single(run.Messages[2].Links).Label);
        Assert.Empty(run.Messages[4].Links);
        Assert.Null(run.Messages[4].JobId);
    }

    [Fact]
    public async Task Execute_StopsAtTheStepLimit()
    {
        var started = await StartAsync("loop forever");
        var entries = new List<AgentEntry>();
        for (var i = 0; i < AgentRun.DefaultMaxSteps + 1; i++)
        {
            entries.Add(new AgentEntry
            {
                Role = AgentMessageRole.Assistant,
                Text = "",
                ToolCalls = [new AgentToolCall
                {
                    Id = $"call-{i}",
                    Name = "list_courses",
                    Arguments = "{}",
                }],
            });
            entries.Add(new AgentEntry
            {
                Role = AgentMessageRole.Tool,
                Text = "- Java II",
                ToolCallId = $"call-{i}",
                ToolName = "list_courses",
            });
        }

        _model.Reply = new AgentReply { Model = "fake-model", Entries = entries };

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().GetAsync(started.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.StartsWith($"The assistant stopped after {AgentRun.DefaultMaxSteps} steps", run.Error);
        Assert.Equal(AgentMessageRole.Tool, run.Messages[^1].Role); // every tool call kept its result
    }

    [Fact]
    public async Task Cancel_StopsAQueuedRunAndItsJob()
    {
        var started = await StartAsync("hi");

        var run = (await Service().CancelAsync(started.Id, TestContext.Current.CancellationToken))!;

        Assert.Equal(AgentRunStatus.Canceled, run.Status);
        Assert.Equal(JobStatus.Canceled, Assert.Single(_jobs.Jobs).Status);
    }

    [Fact]
    public async Task Cancel_StopsAWaitingRun_ButNotTheJobsItStarted()
    {
        var started = await StartAsync("grade and wait");
        var grading = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload { VersionId = Guid.NewGuid() }), DateTimeOffset.UtcNow);
        _jobs.Jobs.Add(grading);
        _model.OnTool = _ => _runContext.RequestWait([grading.Id]);
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [
                new AgentEntry
                {
                    Role = AgentMessageRole.Assistant,
                    Text = "",
                    ToolCalls = [new AgentToolCall
                    {
                        Id = "call-1",
                        Name = "wait_for_jobs",
                        Arguments = "{}",
                    }],
                },
                new AgentEntry { Role = AgentMessageRole.Tool, Text = "Waiting", ToolCallId = "call-1", ToolName = "wait_for_jobs" },
            ],
        };
        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        var run = (await Service().CancelAsync(started.Id, TestContext.Current.CancellationToken))!;

        Assert.Equal(AgentRunStatus.Canceled, run.Status);
        Assert.Equal(JobStatus.Queued, grading.Status);
    }

    [Fact]
    public async Task Execute_DoesNothing_ForAFinishedRun()
    {
        var started = await StartAsync("hi");
        _model.Reply = new AgentReply
        {
            Model = "fake-model",
            Entries = [new AgentEntry { Role = AgentMessageRole.Assistant, Text = "Hello!" }],
        };
        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        await Service().ExecuteAsync(started.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, _model.Calls);
    }

    private sealed class ScriptedModel : ILanguageModel
    {
        public AgentReply Reply { get; set; } = new() { Model = "fake-model", Entries = [] };

        public Exception? Failure { get; set; }

        public Action<AgentEntry>? OnTool { get; set; }

        public int Calls { get; private set; }

        public string? System { get; private set; }

        public IReadOnlyList<AgentEntry>? History { get; private set; }

        public IReadOnlyList<object>? Tools { get; private set; }

        public string ModelId => "fake-model";

        public string GradingModelId => "fake-grading-model";


        public string AdapterModelId => "adapter-model";

        public async Task<AgentReply> RunAgentAsync(
            string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            System = systemPrompt;
            History = history.ToList();
            Tools = tools;
            if (Failure is not null)
            {
                throw Failure;
            }

            foreach (var entry in Reply.Entries)
            {
                if (hooks is null)
                {
                    break;
                }

                if (entry.Role == AgentMessageRole.Tool)
                {
                    OnTool?.Invoke(entry);
                }

                await hooks.OnStep(entry);
                if (entry.Role == AgentMessageRole.Tool && hooks.ShouldStop())
                {
                    return new AgentReply
                    {
                        Model = Reply.Model,
                        Entries = Reply.Entries.TakeWhile(e => e != entry).Append(entry).ToList(),
                    };
                }
            }

            return Reply;
        }

        public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> ChatAsync(
            string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> GradeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LanguageModelReply> AnalyzeAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<LanguageModelReply> WriteAdapterAsync(

            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>

            throw new NotSupportedException();


        public Task<LanguageModelReply> DesignTestsAsync(
            string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
