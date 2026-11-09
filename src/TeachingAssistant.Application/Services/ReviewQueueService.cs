using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class ReviewQueueService : IReviewQueueService
{
    private readonly IReviewQueueStore _store;

    public ReviewQueueService(IReviewQueueStore store)
    {
        _store = store;
    }

    public async Task<ReviewQueueResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var latestVersions = await _store.GetLatestVersionsAsync(SubmissionSource.GoogleClassroom, cancellationToken);
        var drafts = await _store.GetDraftsAsync(SubmissionSource.GoogleClassroom, cancellationToken);

        var latestVersionIds = latestVersions.Select(v => v.VersionId).ToHashSet();
        var newestDrafts = drafts
            .GroupBy(d => d.VersionId)
            .Select(group => group.MaxBy(d => d.CreatedAt)!)
            .ToList();
        var currentDrafts = newestDrafts
            .Where(d => d.ReportStatus != ReportStatus.Approved)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ReviewDraftItem(d.GradingId, d.VersionId, d.VersionNumber, latestVersionIds.Contains(d.VersionId),
                d.CourseId, d.AssignmentId, d.AssignmentTitle, d.SubmissionId, d.StudentId, d.StudentName,
                d.Score, d.MaxScore, d.Bonus, d.Major, d.Minor, d.Style, d.CreatedAt, d.ReportStatus))
            .ToList();

        var draftedVersionIds = newestDrafts.Select(d => d.VersionId).ToHashSet();
        var ungraded = latestVersions
            .Where(v => v.State == "TURNED_IN")
            .Where(v => v.VersionStatus != VersionStatus.Failed)
            .Where(v => !draftedVersionIds.Contains(v.VersionId))
            .OrderBy(v => v.FetchedAt)
            .Select(v => new UngradedSubmissionItem(v.VersionId, v.VersionNumber, v.CourseId, v.AssignmentId, v.AssignmentTitle,
                v.SubmissionId, v.StudentId, v.StudentName, v.Late, v.FetchedAt))
            .ToList();

        return new ReviewQueueResponse(currentDrafts, ungraded);
    }
}
