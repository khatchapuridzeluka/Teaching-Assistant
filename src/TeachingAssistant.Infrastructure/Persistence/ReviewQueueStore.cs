using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

public class ReviewQueueStore(AppDbContext db) : IReviewQueueStore
{
    public async Task<IReadOnlyList<LatestVersionRow>> GetLatestVersionsAsync(
        SubmissionSource source, CancellationToken cancellationToken = default)
    {
        var rows =
            from version in db.SubmissionVersions
            join submission in db.Submissions on version.SubmissionId equals submission.Id
            where submission.Source == source
                  && version.Number == db.SubmissionVersions.Where(v => v.SubmissionId == submission.Id).Max(v => v.Number)
            join assignment in db.CourseAssignments
                on new { submission.Source, submission.CourseId, Id = submission.AssignmentId }
                equals new { assignment.Source, assignment.CourseId, Id = assignment.ExternalAssignmentId } into assignments
            from assignment in assignments.DefaultIfEmpty()
            select new LatestVersionRow
            {
                VersionId = version.Id,
                VersionNumber = version.Number,
                VersionStatus = version.Status,
                FetchedAt = version.FetchedAt,
                CourseId = submission.CourseId,
                AssignmentId = submission.AssignmentId,
                AssignmentTitle = assignment == null ? null : assignment.Title,
                SubmissionId = submission.ExternalSubmissionId,
                StudentId = submission.StudentId,
                StudentName = submission.StudentName,
                State = submission.State,
                Late = submission.Late,
            };

        return await rows.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DraftRow>> GetDraftsAsync(SubmissionSource source, CancellationToken cancellationToken = default)
    {
        var rows =
            from grading in db.Gradings
            join version in db.SubmissionVersions on grading.VersionId equals version.Id
            join submission in db.Submissions on version.SubmissionId equals submission.Id
            where submission.Source == source && grading.Status == GradingStatus.Draft
            join assignment in db.CourseAssignments
                on new { submission.Source, submission.CourseId, Id = submission.AssignmentId }
                equals new { assignment.Source, assignment.CourseId, Id = assignment.ExternalAssignmentId } into assignments
            from assignment in assignments.DefaultIfEmpty()
            select new DraftRow
            {
                GradingId = grading.Id,
                VersionId = grading.VersionId,
                VersionNumber = version.Number,
                CourseId = submission.CourseId,
                AssignmentId = submission.AssignmentId,
                AssignmentTitle = assignment == null ? null : assignment.Title,
                SubmissionId = submission.ExternalSubmissionId,
                StudentId = submission.StudentId,
                StudentName = submission.StudentName,
                Score = grading.Score,
                MaxScore = grading.MaxScore,
                Bonus = grading.Bonus,
                Major = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Major),
                Minor = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor),
                Style = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Style),
                CreatedAt = grading.CreatedAt,
                ReportStatus = db.GradingReports.Where(r => r.GradingId == grading.Id).Select(r => (ReportStatus?)r.Status).FirstOrDefault(),
            };

        return await rows.ToListAsync(cancellationToken);
    }
}
