using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Evaluation runs: the approved reports graded again on the side by chosen models, compared with the teacher's reports.
/// Starting a run stores one pending result per report and model and queues one background grading for each; the comparison
/// uses each report's newest finished result per model.
/// </summary>
public class EvaluationRunService : IEvaluationRunService
{
    /// <summary>The models a run can use, most capable first.</summary>
    public static readonly IReadOnlyList<string> AvailableModels = GradingModels.Available;

    private readonly IEvaluationRunStore _runs;
    private readonly IGradingReportStore _reports;
    private readonly IJobService _jobs;
    private readonly IJobStore _jobStore;
    private readonly ILanguageModel _languageModel;
    private readonly TimeProvider _timeProvider;

    public EvaluationRunService(
        IEvaluationRunStore runs, IGradingReportStore reports, IJobService jobs, IJobStore jobStore, ILanguageModel languageModel,
        TimeProvider timeProvider)
    {
        _jobStore = jobStore;
        _runs = runs;
        _reports = reports;
        _jobs = jobs;
        _languageModel = languageModel;
        _timeProvider = timeProvider;
    }

    public async Task<ModelComparisonResponse> StartAsync(StartEvaluationRunRequest request, CancellationToken cancellationToken = default)
    {
        var models = request.Models.Select(m => m.Trim()).Where(m => m.Length > 0).Distinct().ToList();
        if (models.Count == 0)
        {
            throw new InvalidRequestException("Choose at least one model.");
        }

        var unknown = models.Where(m => !AvailableModels.Contains(m)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidRequestException($"Unknown model: {string.Join(", ", unknown)}. Choose from {string.Join(", ", AvailableModels)}.");
        }

        var reports = (await _reports.GetApprovedAsync(request.CourseId, cancellationToken))
            .Where(a => a.Draft.Status != GradingStatus.Discarded)
            .ToList();
        if (reports.Count == 0)
        {
            throw new InvalidRequestException("There is no approved report to compare with yet. Review and approve some drafts first.");
        }

        var now = _timeProvider.GetUtcNow();
        var run = new EvaluationRun(request.CourseId, models, now);
        var results = reports
            .SelectMany(a => models.Select(model => new EvaluationRunResult(run.Id, a.Report.Id, a.Report.VersionId, model, now)))
            .ToList();
        _runs.Add(run, results);
        await _runs.SaveChangesAsync(cancellationToken);

        try
        {
            await _jobs.QueueEvaluationRunAsync(run.Id, results.Select(r => r.Id).ToList(), request.CourseId, cancellationToken);
        }
        catch
        {
            // Without its jobs the run would wait as pending forever and block starting another one.
            await _runs.DeleteAsync(run.Id, CancellationToken.None);
            throw;
        }

        return await GetAsync(request.CourseId, cancellationToken);
    }

    public async Task<ModelComparisonResponse> GetAsync(string? courseId = null, CancellationToken cancellationToken = default)
    {
        var approved = (await _reports.GetApprovedAsync(courseId, cancellationToken))
            .Where(a => a.Draft.Status != GradingStatus.Discarded)
            .ToDictionary(a => a.Report.Id);

        var latestRun = await _runs.GetLatestAsync(courseId, cancellationToken);
        EvaluationRunSummary? summary = null;
        if (latestRun is not null)
        {
            var runResults = await _runs.GetResultsAsync(latestRun.Id, cancellationToken);
            await FailOrphanedAsync(latestRun, runResults, cancellationToken);
            summary = new EvaluationRunSummary(latestRun.Id, latestRun.CreatedAt, latestRun.Models, runResults.Count,
                runResults.Count(r => r.Status == RunResultStatus.Done), runResults.Count(r => r.Status == RunResultStatus.Failed),
                runResults.Count(r => r.Status == RunResultStatus.Pending));
        }

        // The newest finished result per report and model, for the reports that still count.
        var newest = (await _runs.GetDoneAsync(cancellationToken))
            .Where(r => approved.ContainsKey(r.ReportId))
            .GroupBy(r => (r.ReportId, r.Model))
            .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
            .ToList();
        var failed = (await _runs.GetFailedAsync(cancellationToken)).Where(r => approved.ContainsKey(r.ReportId)).ToList();

        var rows = newest
            .GroupBy(r => r.Model)
            .Select(g => Row(g.Key,
                g.Select(r => (Result: r, Comparison: RunComparison.Of(r, approved[r.ReportId].Report, approved[r.ReportId].Draft.MaxScore))).ToList(),
                failed.Count(f => f.Model == g.Key && !g.Any(r => r.ReportId == f.ReportId))))
            .OrderBy(r => AvailableModels.ToList().IndexOf(r.Model) is var i && i < 0 ? int.MaxValue : i)
            .ToList();

        return new ModelComparisonResponse(AvailableModels, _languageModel.GradingModelId, approved.Count, summary, rows);
    }

    /// <summary>
    /// Pending results with no running job behind them will never be graded (their jobs were never queued or are gone), so
    /// they are marked failed; otherwise the run would show as running forever and block starting another one.
    /// </summary>
    private async Task FailOrphanedAsync(EvaluationRun run, IReadOnlyList<EvaluationRunResult> results, CancellationToken cancellationToken)
    {
        // A run just started may not have its jobs queued yet.
        var now = _timeProvider.GetUtcNow();
        if (now - run.CreatedAt < TimeSpan.FromMinutes(1) || !results.Any(r => r.Status == RunResultStatus.Pending))
        {
            return;
        }

        var running = (await _jobStore.GetOpenGroupsAsync(cancellationToken))
            .Any(j => j.Type == Domain.Jobs.JobType.EvaluationRun && JobPayloads.Read<EvaluationRunPayload>(j.Payload).RunId == run.Id);
        if (running)
        {
            return;
        }

        foreach (var result in results.Where(r => r.Status == RunResultStatus.Pending))
        {
            result.Fail("Never started: no grading job was running for it.", now);
        }

        await _runs.SaveChangesAsync(cancellationToken);
    }

    internal static ModelComparisonRow Row(
        string model, IReadOnlyList<(EvaluationRunResult Result, RunComparison Comparison)> results, int failed)
    {
        var differences = results.Select(r => r.Comparison.DifferencePercent).OfType<double>().ToList();
        double? meanGap = differences.Count == 0 ? null : differences.Average(Math.Abs);
        var durations = results.Select(r => r.Result.DurationMs).OfType<int>().ToList();
        var teacherMistakes = results.Sum(r => r.Comparison.TeacherMistakes);
        var found = results.Sum(r => r.Comparison.Found);

        return new ModelComparisonRow(
            model,
            results.Count,
            differences.Count,
            meanGap is { } gap ? Math.Max(0, 100 - gap) : null,
            differences.Count(d => Math.Abs(d) <= EvaluationService.TolerancePercent + 1e-9),
            differences.Count == 0 ? null : differences.Average(),
            teacherMistakes,
            found,
            teacherMistakes == 0 ? null : (double)found / teacherMistakes * 100,
            results.Sum(r => r.Comparison.RepeatedFalseAlarms),
            results.Sum(r => r.Comparison.Unreviewed),
            durations.Count == 0 ? null : durations.Average() / 1000,
            failed);
    }
}
