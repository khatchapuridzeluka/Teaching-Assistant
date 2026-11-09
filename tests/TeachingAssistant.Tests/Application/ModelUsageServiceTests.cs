using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Tests.Application;

public class ModelUsageServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCalls _calls = new();
    private readonly FakeSubjects _subjects = new();

    private ModelUsageService Service() => new(_calls, _subjects, new FixedClock(Now));

    [Fact]
    public async Task Totals_AveragePerAnsweredCall_AndCacheHitRateIsCachedShareOfThePrompt()
    {
        var versionId = Guid.NewGuid();
        _subjects.Subjects.Add(new JobSubject(versionId, 1, "course", "assignment", "Assignment 2", "sub-1", "student-1", "Ana"));
        var subject = new ModelCallSubject("course", "assignment", versionId);
        _calls.Calls.Add(ModelCall.Success(ModelCallPurpose.Grading, "opus", "opus", new ModelCallTokens(2000, 1000, 6000, 2000), Now.AddHours(-1),
            TimeSpan.FromSeconds(20), subject));
        _calls.Calls.Add(ModelCall.Success(ModelCallPurpose.Grading, "opus", "opus", new ModelCallTokens(2000, 3000, 8000, 0), Now.AddDays(-1),
            TimeSpan.FromSeconds(40), subject));
        _calls.Calls.Add(ModelCall.Failure(ModelCallPurpose.Grading, "opus", "Rate limited", Now.AddDays(-1), TimeSpan.FromSeconds(3), subject));

        var usage = await Service().GetAsync(7, TestContext.Current.CancellationToken);

        var grading = Assert.Single(usage.Totals);
        Assert.Equal((3, 1), (grading.Calls, grading.Failed));
        Assert.Equal((10000L, 2000L), (grading.AveragePromptTokens, grading.AverageOutputTokens));
        Assert.Equal(0.7, grading.CacheHitRate!.Value, 3);
        Assert.Equal(7, usage.Days.Count);
        Assert.Equal((1, 2000L), (usage.Days[^1].Calls, usage.Days[^1].InputTokens));
        Assert.Equal(("Ana", "Assignment 2"), (usage.Recent[0].StudentName, usage.Recent[0].AssignmentTitle));
    }

    [Fact]
    public async Task Days_OutsideOneToAYear_AreRefused()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => Service().GetAsync(0, TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCalls : IModelCallStore
    {
        public List<ModelCall> Calls { get; } = [];

        public Task RecordAsync(ModelCall call, CancellationToken cancellationToken = default)
        {
            Calls.Add(call);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ModelCall>> GetSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModelCall>>(Calls.Where(c => c.CreatedAt >= since).OrderByDescending(c => c.CreatedAt).ToList());
    }

    private sealed class FakeSubjects : IJobStore
    {
        public List<JobSubject> Subjects { get; } = [];

        public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobSubject>>(Subjects.Where(s => versionIds.Contains(s.VersionId)).ToList());

        public Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Job>> GetRecentAsync(
            JobStatus? status, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(DateTimeOffset finishedSince, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
