using System.ComponentModel;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Tools;

public class AgentTools
{
    private readonly IJobStore _jobs;
    private readonly JobReporter _reporter;
    private readonly AgentRunContext _context;

    public AgentTools(IJobStore jobs, JobReporter reporter, AgentRunContext context)
    {
        _jobs = jobs;
        _reporter = reporter;
        _context = context;
    }

    [AgentTool("wait_for_jobs")]
    [Description(
        "Waits until background jobs you started (gradings and other work; their job ids are in the tools' answers) have finished, "
        + "when your next step needs their results. Start all the work you need first, then wait once for all of it. If they are "
        + "still running, this ends your turn: you are woken with a short result of every job when the last one finishes, or "
        + "after 30 minutes with their progress so far. Do not call other tools together with it.")]
    public async Task<string> WaitForJobsAsync(
        [Description("The job ids to wait for")] string[] jobIds, CancellationToken cancellationToken)
    {
        if (_context.RunId is null)
        {
            return "Waiting is not possible here; tell the teacher the work continues in the background.";
        }

        var jobs = new List<Job>();
        foreach (var text in jobIds.Distinct())
        {
            if (!Guid.TryParse(text, out var id) || await _jobs.FindAsync(id, cancellationToken) is not { } job)
            {
                return $"No background job has the id '{text}'. Use the job ids from the tools' answers.";
            }

            jobs.Add(job);
        }

        if (jobs.Count == 0)
        {
            return "Give at least one job id to wait for.";
        }

        var running = jobs.Where(j => !j.IsFinished).Select(j => j.Id).ToList();
        if (running.Count == 0)
        {
            _context.RecordLinks(await _reporter.LinksAsync(jobs, cancellationToken));
            return $"All of these jobs have already finished:\n{await _reporter.DescribeAsync(jobs, cancellationToken)}";
        }

        _context.RequestWait(running);
        return $"Waiting for {running.Count} {(running.Count == 1 ? "job" : "jobs")} still in progress. This turn ends now; you will be "
            + $"woken with their results when they finish, or after {(int)AgentRun.WaitLimit.TotalMinutes} minutes with their progress.";
    }
}
