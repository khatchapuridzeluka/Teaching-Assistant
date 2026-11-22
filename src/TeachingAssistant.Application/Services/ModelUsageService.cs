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
            series.Add(new ModelUsageDay
            {
                Date = day,
                Calls = dayCalls.Count,
                InputTokens = dayCalls.Sum(c => c.InputTokens ?? 0),
                CacheReadTokens = dayCalls.Sum(c => c.CacheReadTokens ?? 0),
                CacheWriteTokens = dayCalls.Sum(c => c.CacheWriteTokens ?? 0),
                OutputTokens = dayCalls.Sum(c => c.OutputTokens ?? 0),
            });
        }

        var recent = calls.Take(RecentCalls).ToList();
        var versionIds = recent.Where(c => c.VersionId is not null).Select(c => c.VersionId!.Value).Distinct().ToList();
        var subjects = (await _subjects.GetSubjectsAsync(versionIds, cancellationToken)).ToDictionary(s => s.VersionId);

        var recentResponses = recent
            .Select(c =>
            {
                var subject = c.VersionId is { } versionId ? subjects.GetValueOrDefault(versionId) : null;
                return new ModelCallResponse
                {
                    Id = c.Id,
                    Purpose = c.Purpose,
                    RequestedModel = c.RequestedModel,
                    Model = c.Model,
                    Succeeded = c.Succeeded,
                    Error = c.Error,
                    InputTokens = c.InputTokens,
                    OutputTokens = c.OutputTokens,
                    CacheReadTokens = c.CacheReadTokens,
                    CacheWriteTokens = c.CacheWriteTokens,
                    DurationMs = c.DurationMs,
                    CourseId = c.CourseId,
                    AssignmentId = c.AssignmentId,
                    AssignmentTitle = subject?.AssignmentTitle,
                    VersionId = c.VersionId,
                    SubmissionId = subject?.SubmissionId,
                    StudentId = subject?.StudentId,
                    StudentName = subject?.StudentName,
                    CreatedAt = c.CreatedAt,
                };
            })
            .ToList();

        return new ModelUsageResponse
        {
            Since = since,
            Purpose = purpose,
            Summary = summary,
            Totals = totals,
            Days = series,
            Recent = recentResponses,
        };
    }

    private static ModelUsageTotal Total(ModelCallPurpose purpose, List<ModelCall> calls)
    {
        var s = Summarize(calls);
        return new ModelUsageTotal
        {
            Purpose = purpose,
            Calls = s.Calls,
            Failed = s.Failed,
            InputTokens = s.InputTokens,
            OutputTokens = s.OutputTokens,
            CacheReadTokens = s.CacheReadTokens,
            CacheWriteTokens = s.CacheWriteTokens,
            AveragePromptTokens = s.AveragePromptTokens,
            AverageOutputTokens = s.AverageOutputTokens,
            CacheHitRate = s.CacheHitRate,
            AverageDurationMs = s.AverageDurationMs,
        };
    }

    private static ModelUsageSummary Summarize(List<ModelCall> calls)
    {
        var input = calls.Sum(c => c.InputTokens ?? 0);
        var output = calls.Sum(c => c.OutputTokens ?? 0);
        var cacheRead = calls.Sum(c => c.CacheReadTokens ?? 0);
        var cacheWrite = calls.Sum(c => c.CacheWriteTokens ?? 0);
        var answered = calls.Count(c => c.Succeeded);
        var prompt = input + cacheRead + cacheWrite;

        return new ModelUsageSummary
        {
            Calls = calls.Count,
            Failed = calls.Count - answered,
            InputTokens = input,
            OutputTokens = output,
            CacheReadTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
            AveragePromptTokens = answered == 0 ? 0 : prompt / answered,
            AverageOutputTokens = answered == 0 ? 0 : output / answered,
            CacheHitRate = prompt == 0 ? null : (double)cacheRead / prompt,
            AverageDurationMs = calls.Count == 0 ? 0 : (long)calls.Average(c => c.DurationMs),
        };
    }
}
