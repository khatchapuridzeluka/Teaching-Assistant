using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class ReviewQueueServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeStore _store = new();

    [Fact]
    public async Task Drafts_ShowOnlyTheNewestDraftPerVersion_OldestWaitingFirst()
    {
        var v1 = _store.AddLatest("ana", "TURNED_IN");
        var v2 = _store.AddLatest("beka", "TURNED_IN");
        _store.AddDraft(v1, Start.AddHours(1));
        var newest = _store.AddDraft(v1, Start.AddHours(3));
        var beka = _store.AddDraft(v2, Start.AddHours(2));

        var queue = await new ReviewQueueService(_store).GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal([beka, newest], queue.Drafts.Select(d => d.GradingId));
    }

    [Fact]
    public async Task Ungraded_IsTurnedInWorkWithoutADraft_NotReturnedOrFailed()
    {
        var drafted = _store.AddLatest("ana", "TURNED_IN");
        _store.AddDraft(drafted, Start);
        var waiting = _store.AddLatest("beka", "TURNED_IN");
        _store.AddLatest("nini", "RETURNED");
        _store.AddLatest("saba", "TURNED_IN", VersionStatus.Failed);

        var queue = await new ReviewQueueService(_store).GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal([waiting], queue.Ungraded.Select(u => u.VersionId));
    }

    [Fact]
    public async Task DraftOnAnOlderVersion_IsMarkedOutdated_AndTheNewVersionWaits()
    {
        var oldVersion = Guid.NewGuid();
        _store.AddDraft(oldVersion, Start);
        var newVersion = _store.AddLatest("ana", "TURNED_IN");

        var queue = await new ReviewQueueService(_store).GetAsync(TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(queue.Drafts).IsLatestVersion);
        Assert.Equal(newVersion, Assert.Single(queue.Ungraded).VersionId);
    }

    [Fact]
    public async Task ApprovedReport_LeavesTheQueue_InReviewStays_AndNeitherCountsAsUngraded()
    {
        var approved = _store.AddLatest("ana", "TURNED_IN");
        _store.AddDraft(approved, Start, ReportStatus.Approved);
        var inReview = _store.AddLatest("beka", "TURNED_IN");
        var editing = _store.AddDraft(inReview, Start, ReportStatus.InReview);

        var queue = await new ReviewQueueService(_store).GetAsync(TestContext.Current.CancellationToken);

        var draft = Assert.Single(queue.Drafts);
        Assert.Equal((editing, (ReportStatus?)ReportStatus.InReview), (draft.GradingId, draft.ReportStatus));
        Assert.Empty(queue.Ungraded);
    }

    private sealed class FakeStore : IReviewQueueStore
    {
        private readonly List<LatestVersionRow> _latest = [];
        private readonly List<DraftRow> _drafts = [];

        public Guid AddLatest(string student, string state, VersionStatus status = VersionStatus.Ready)
        {
            var id = Guid.NewGuid();
            _latest.Add(new LatestVersionRow(id, 1, status, Start.AddMinutes(_latest.Count), "course", "assignment", "Assignment",
                $"sub-{student}", student, student, state, false));
            return id;
        }

        public Guid AddDraft(Guid versionId, DateTimeOffset createdAt, ReportStatus? report = null)
        {
            var id = Guid.NewGuid();
            _drafts.Add(new DraftRow(id, versionId, 1, "course", "assignment", "Assignment", "sub", "student", "Student",
                80, 100, null, 1, 2, 3, createdAt, report));
            return id;
        }

        public Task<IReadOnlyList<LatestVersionRow>> GetLatestVersionsAsync(SubmissionSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LatestVersionRow>>(_latest);

        public Task<IReadOnlyList<DraftRow>> GetDraftsAsync(SubmissionSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DraftRow>>(_drafts);
    }
}
