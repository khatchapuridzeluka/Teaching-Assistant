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
            .Select(r => new GradingSummary(
                r.grading.Id,
                r.grading.VersionId,
                r.version.Number,
                r.submission.AssignmentId,
                r.submission.ExternalSubmissionId,
                r.submission.StudentId,
                r.submission.StudentName,
                r.grading.Status,
                r.grading.Score,
                r.grading.MaxScore,
                r.grading.Bonus,
                r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Major),
                r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor),
                r.grading.Mistakes.Count(m => m.Severity == MistakeSeverity.Style),
                r.grading.Model,
                r.grading.CreatedAt,
                db.GradingReports.Where(report => report.GradingId == r.grading.Id).Select(report => (ReportStatus?)report.Status).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public void Add(AiGrading grading) => db.Gradings.Add(grading);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
