using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Services;

public class ModelUsageService : IModelUsageService
{
    private const int RecentCalls = 200;

    private readonly IModelCallStore _calls;
    private readonly IJobStore _subjects;
    private readonly TimeProvider _timeProvider;

    public ModelUsageService(IModelCallStore calls, IJobStore subjects, TimeProvider timeProvider)
    {
        _calls = calls;
        _subjects = subjects;
        _timeProvider = timeProvider;
    }

    public async Task<ModelUsageResponse> GetAsync(int days, ModelCallPurpose? purpose, CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 365)
        {
            throw new InvalidRequestException("Days must be between 1 and 365.");
        }

        var now = _timeProvider.GetUtcNow();
        var since = now.AddDays(-days);
        var all = await _calls.GetSinceAsync(since, cancellationToken);

        var totals = all
            .GroupBy(c => c.Purpose)
            .OrderBy(g => g.Key)
            .Select(g => Total(g.Key, g.ToList()))
            .ToList();

        var calls = purpose is { } wanted ? all.Where(c => c.Purpose == wanted).ToList() : all.ToList();
        var summary = Summarize(calls);

        var byDay = calls.ToLookup(c => DateOnly.FromDateTime(c.CreatedAt.UtcDateTime));
        var firstDay = DateOnly.FromDateTime(since.UtcDateTime).AddDays(1);
        var lastDay = DateOnly.FromDateTime(now.UtcDateTime);
        var series = new List<ModelUsageDay>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var dayCalls = byDay[day].ToList();
            series.Add(new ModelUsageDay(day, dayCalls.Count, dayCalls.Sum(c => c.InputTokens ?? 0),
                dayCalls.Sum(c => c.CacheReadTokens ?? 0), dayCalls.Sum(c => c.CacheWriteTokens ?? 0),
                dayCalls.Sum(c => c.OutputTokens ?? 0)));
        }

        var recent = calls.Take(RecentCalls).ToList();
        var versionIds = recent.Where(c => c.VersionId is not null).Select(c => c.VersionId!.Value).Distinct().ToList();
        var subjects = (await _subjects.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);

        var recentResponses = recent
            .Select(c =>
            {
                var subject = c.VersionId is { } versionId ? subjects.GetValueOrDefault(versionId) : null;
                return new ModelCallResponse(c.Id, c.Purpose, c.RequestedModel, c.Model, c.Succeeded, c.Error, c.InputTokens,
                    c.OutputTokens, c.CacheReadTokens, c.CacheWriteTokens, c.DurationMs, c.CourseId, c.AssignmentId,
                    subject?.AssignmentTitle, c.VersionId, subject?.SubmissionId, subject?.StudentId, subject?.StudentName, c.CreatedAt);
            })
            .ToList();

        return new ModelUsageResponse(since, purpose, summary, totals, series, recentResponses);
    }

    private static ModelUsageTotal Total(ModelCallPurpose purpose, List<ModelCall> calls)
    {
        var s = Summarize(calls);
        return new ModelUsageTotal(purpose, s.Calls, s.Failed, s.InputTokens, s.OutputTokens, s.CacheReadTokens, s.CacheWriteTokens,
            s.AveragePromptTokens, s.AverageOutputTokens, s.CacheHitRate, s.AverageDurationMs);
    }

    private static ModelUsageSummary Summarize(List<ModelCall> calls)
    {
        var input = calls.Sum(c => c.InputTokens ?? 0);
        var output = calls.Sum(c => c.OutputTokens ?? 0);
        var cacheRead = calls.Sum(c => c.CacheReadTokens ?? 0);
        var cacheWrite = calls.Sum(c => c.CacheWriteTokens ?? 0);
        var answered = calls.Count(c => c.Succeeded);
        var prompt = input + cacheRead + cacheWrite;

        return new ModelUsageSummary(
            calls.Count,
            calls.Count - answered,
            input,
            output,
            cacheRead,
            cacheWrite,
            answered == 0 ? 0 : prompt / answered,
            answered == 0 ? 0 : output / answered,
            prompt == 0 ? null : (double)cacheRead / prompt,
            calls.Count == 0 ? 0 : (long)calls.Average(c => c.DurationMs));
    }
}
