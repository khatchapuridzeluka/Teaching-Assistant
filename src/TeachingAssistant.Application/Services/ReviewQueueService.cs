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
            .Select(d => new ReviewDraftItem
            {
                GradingId = d.GradingId,
                VersionId = d.VersionId,
                VersionNumber = d.VersionNumber,
                IsLatestVersion = latestVersionIds.Contains(d.VersionId),
                CourseId = d.CourseId,
                AssignmentId = d.AssignmentId,
                AssignmentTitle = d.AssignmentTitle,
                SubmissionId = d.SubmissionId,
                StudentId = d.StudentId,
                StudentName = d.StudentName,
                Score = d.Score,
                MaxScore = d.MaxScore,
                Bonus = d.Bonus,
                Major = d.Major,
                Minor = d.Minor,
                Style = d.Style,
                CreatedAt = d.CreatedAt,
                ReportStatus = d.ReportStatus,
            })
            .ToList();

        var draftedVersionIds = newestDrafts.Select(d => d.VersionId).ToHashSet();
        var ungraded = latestVersions
            .Where(v => v.State == "TURNED_IN")
            .Where(v => v.VersionStatus != VersionStatus.Failed)
            .Where(v => !draftedVersionIds.Contains(v.VersionId))
            .OrderBy(v => v.FetchedAt)
            .Select(v => new UngradedSubmissionItem
            {
                VersionId = v.VersionId,
                VersionNumber = v.VersionNumber,
                CourseId = v.CourseId,
                AssignmentId = v.AssignmentId,
                AssignmentTitle = v.AssignmentTitle,
                SubmissionId = v.SubmissionId,
                StudentId = v.StudentId,
                StudentName = v.StudentName,
                Late = v.Late,
                FetchedAt = v.FetchedAt,
            })
            .ToList();

        return new ReviewQueueResponse { Drafts = currentDrafts, Ungraded = ungraded };
    }
}
