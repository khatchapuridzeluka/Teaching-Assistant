using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence;

public class StudentMistakeStore(AppDbContext db) : IStudentMistakeStore
{
    public async Task<IReadOnlyList<StudentMistake>> GetByReportAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        await db.StudentMistakes
            .Where(m => m.ReportId == reportId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentMistake>> GetByStudentAsync(
        string courseId, string studentId, CancellationToken cancellationToken = default) =>
        await db.StudentMistakes
            .Where(m => m.CourseId == courseId && m.StudentId == studentId)
            .OrderByDescending(m => m.ApprovedAt)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<StudentMistake> mistakes) => db.StudentMistakes.AddRange(mistakes);

    public void RemoveRange(IEnumerable<StudentMistake> mistakes) => db.StudentMistakes.RemoveRange(mistakes);
}
