using System.ComponentModel;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Tools;

public class GradingTools
{
    private readonly ISubmissionFetchService _fetchService;
    private readonly IJobService _jobService;
    private readonly IGradingStore _gradings;
    private readonly AgentRunContext _context;
    private readonly TimeProvider _timeProvider;

    public GradingTools(
        ISubmissionFetchService fetchService, IJobService jobService, IGradingStore gradings, AgentRunContext context, TimeProvider timeProvider)
    {
        _fetchService = fetchService;
        _jobService = jobService;
        _gradings = gradings;
        _context = context;
        _timeProvider = timeProvider;
    }

    [AgentTool("analyze_common_mistakes")]
    [Description(
        "Starts the class-wide Common mistakes analysis of one assignment: one paid AI call that reads every student's current AI "
        + "draft (no names; the drafts do not need to be approved), writes a class overview, groups similar mistakes and suggests "
        + "what to cover in the next lecture. It runs in the background and returns a job id at once; wait for it if you need the "
        + "result, which then includes the overview and the groups. If you just started gradings for this assignment, wait for "
        + "them first so their drafts are included. Use it when the teacher asks for the common mistakes.")]
    public async Task<string> AnalyzeCommonMistakesAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        CancellationToken cancellationToken = default)
    {
        var drafts = (await _gradings.GetSummariesAsync(SubmissionSource.GoogleClassroom, courseId, assignmentId, null, cancellationToken))
            .Count(g => g.Status == GradingStatus.Draft);
        if (drafts == 0)
        {
            return "Not started: no student of this assignment has a current AI draft (none were graded yet, or every draft was "
                + "discarded), and the analysis reads the drafts. The step that provides them is grade_all_students for this "
                + "assignment (regrade is not needed, since no current draft exists): one paid grading per student who turned in work. "
                + "Offer the teacher to grade them first and then run the analysis; once they agree, grade, wait for the gradings, "
                + "then call this again.";
        }

        var started = _timeProvider.GetUtcNow();
        var job = await _jobService.QueueAnalyzeMistakesAsync(courseId, assignmentId, cancellationToken);
        _context.RecordQueuedJob(job.Id);
        _context.RecordLinks([new AgentLink(AgentLinkKind.CommonMistakes, "Common mistakes", courseId, assignmentId)]);
        if (job.CreatedAt < started)
        {
            return $"A Common mistakes analysis of this assignment is already in progress (job id {job.Id}); nothing new was started. "
                + "Wait for it if you need its result.";
        }

        return $"Common mistakes analysis queued (job id {job.Id}). It runs in the background, usually within a minute; the result "
            + "also appears on the assignment's Common mistakes page.";
    }

    [AgentTool("grade_all_students")]
    [Description(
        "Starts AI gradings for every student of one assignment (like the Grade all button): by default only those whose latest "
        + "version has no AI draft yet. It first fetches the assignment's submissions from Google Classroom, then queues one "
        + "background grading per student under a single job and returns at once with that job id and the counts. Each grading "
        + "is a paid AI call, so use it only when the teacher asks to grade the whole assignment.")]
    public async Task<string> GradeAllStudentsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("true also grades again every student whose latest version already has an AI draft (the new drafts become "
            + "current, the earlier ones stay in the history); set it only when the teacher explicitly asks to grade everyone "
            + "again or regrade")] bool regrade = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _fetchService.FetchAsync(courseId, assignmentId, cancellationToken);
            var result = await _jobService.QueueGradeAssignmentAsync(courseId, assignmentId, regrade, cancellationToken);
            if (result.Job is not null)
            {
                _context.RecordLinks([new AgentLink(AgentLinkKind.Assignment, "The assignment's drafts", courseId, assignmentId)]);
            }

            return Describe(result, regrade);
        }
        catch (Exception ex) when (ex is SubmissionSourceNotConnectedException or SubmissionSourceNotFoundException
            or SubmissionSourceUnavailableException or InvalidRequestException)
        {
            return $"Failed: {ex.Message}";
        }
    }

    private string Describe(GradeAllResponse result, bool regrade)
    {
        var problems = result.Problems.Count == 0 ? "" : $" Could not be graded: {string.Join(" ", result.Problems)}";
        if (result.Job is { } running && result is { Queued: 0, AlreadyQueued: 0 })
        {
            return $"A Grade all for this assignment is already running (job id {running.Id}); nothing new was started.";
        }

        var unstored = $"{result.NoWork} turned nothing in that can be graded, {result.NotFetched} have no stored work.";
        if (result.Job is not { } job)
        {
            var drafted = regrade || result.AlreadyDrafted == 0
                ? ""
                : $" {result.AlreadyDrafted} already have an AI draft; call this again with regrade set to true only if the teacher asks to grade them again.";
            return $"Nothing to grade among the {result.Students} students.{drafted} {unstored}{problems}";
        }

        _context.RecordQueuedJob(job.Id);
        var redrafts = regrade && result.AlreadyDrafted > 0
            ? $" {result.AlreadyDrafted} of them already had a draft, which stays in the history."
            : result.AlreadyDrafted > 0 ? $" {result.AlreadyDrafted} already had an AI draft and were skipped." : "";
        var inProgress = result.AlreadyQueued > 0 ? $" {result.AlreadyQueued} were already being graded and are included." : "";
        return $"{(regrade ? "Grading again" : "Grading")} queued for {result.Queued + result.AlreadyQueued} of {result.Students} students "
            + $"(job id {job.Id}).{redrafts}{inProgress} {unstored}{problems} It runs in the background, about a minute per student; "
            + "the drafts appear on the assignment page for the teacher to review.";
    }
}
