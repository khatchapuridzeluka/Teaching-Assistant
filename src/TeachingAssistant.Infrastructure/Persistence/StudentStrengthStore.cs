using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence;

public class StudentStrengthStore(AppDbContext db) : IStudentStrengthStore
{
    public async Task<IReadOnlyList<StudentStrength>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        await db.StudentStrengths
            .Where(s => s.ReportId == reportId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentStrength>> GetByStudentAsync(
        string courseId, string studentId, CancellationToken cancellationToken = default) =>
        await db.StudentStrengths
            .Where(s => s.CourseId == courseId && s.StudentId == studentId)
            .Where(s => db.Gradings.Any(g => g.Id == s.GradingId && g.Status == GradingStatus.Draft))
            .OrderByDescending(s => s.ApprovedAt)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<StudentStrength> strengths) => db.StudentStrengths.AddRange(strengths);

    public void RemoveRange(IEnumerable<StudentStrength> strengths) => db.StudentStrengths.RemoveRange(strengths);
}
