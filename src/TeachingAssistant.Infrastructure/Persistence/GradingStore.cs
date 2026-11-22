using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

public class GradingStore(AppDbContext db) : IGradingStore
{
    public async Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(
        IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        await db.Gradings
            .Include(g => g.Mistakes)
            .Include(g => g.Strengths)
            .Where(g => versionIds.Contains(g.VersionId))
            .OrderByDescending(g => g.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
        db.Gradings
            .Include(g => g.Mistakes)
            .Include(g => g.Strengths)
            .AsSplitQuery()
            .SingleOrDefaultAsync(g => g.Id == gradingId, cancellationToken);

    public async Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId,
        CancellationToken cancellationToken = default)
    {
        var rows =
            from grading in db.Gradings
            join version in db.SubmissionVersions on grading.VersionId equals version.Id
            join submission in db.Submissions on version.SubmissionId equals submission.Id
            where submission.Source == source && submission.CourseId == courseId
            select new { grading, version, submission };

        if (assignmentId is not null)
        {
            rows = rows.Where(r => r.submission.AssignmentId == assignmentId);
        }

        if (studentId is not null)
        {
            rows = rows.Where(r => r.submission.StudentId == studentId);
        }

        return await rows
            .OrderByDescending(r => r.grading.CreatedAt)
            .Select(r => new GradingSummary
            {
                Id = r.grading.Id,
                VersionId = r.grading.VersionId,
                VersionNumber = r.version.Number,
                AssignmentId = r.submission.AssignmentId,
                SubmissionId = r.submission.ExternalSubmissionId,
                StudentId = r.submission.StudentId,
                StudentName = r.submission.StudentName,
                Status = r.grading.Status,
                Score = r.grading.Score,
                MaxScore = r.grading.MaxScore,
                Bonus = r.grading.Bonus,
                Major = r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Major),
                Minor = r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor),
                Style = r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Style),
                Model = r.grading.Model,
                CreatedAt = r.grading.CreatedAt,
                ReportStatus = db.GradingReports.Where(report => report.GradingId == r.grading.Id).Select(report => (ReportStatus?)report.Status).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default)
    {
        return await (
                from grading in db.Gradings
                join version in db.SubmissionVersions on grading.VersionId equals version.Id
                join submission in db.Submissions on version.SubmissionId equals submission.Id
                where grading.Status == GradingStatus.Discarded && (courseId == null || submission.CourseId == courseId)
                orderby grading.DiscardedAt descending
                select new DiscardedDraft
                {
                    GradingId = grading.Id,
                    CourseId = submission.CourseId,
                    AssignmentId = submission.AssignmentId,
                    StudentId = submission.StudentId,
                    StudentName = submission.StudentName,
                    Model = grading.Model,
                    Score = grading.Score,
                    Bonus = grading.Bonus,
                    MaxScore = grading.MaxScore,
                    Major = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Major),
                    Minor = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor),
                    Style = grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Style),
                    Reason = grading.DiscardReason,
                    Note = grading.DiscardNote,
                    CreatedAt = grading.CreatedAt,
                    DiscardedAt = grading.DiscardedAt,
                    ReportStatus = db.GradingReports.Where(r => r.GradingId == grading.Id).Select(r => (ReportStatus?)r.Status).FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);
    }

    public void Add(AiGrading grading) => db.Gradings.Add(grading);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
