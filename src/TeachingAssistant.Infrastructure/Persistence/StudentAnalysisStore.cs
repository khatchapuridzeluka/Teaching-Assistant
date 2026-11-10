using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence;

public class StudentAnalysisStore(AppDbContext db) : IStudentAnalysisStore
{
    public Task<StudentAnalysis?> GetLatestAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
        db.StudentAnalyses
            .Include(a => a.Groups)
            .Where(a => a.CourseId == courseId && a.StudentId == studentId && a.Kind == kind)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(StudentAnalysis analysis) => db.StudentAnalyses.Add(analysis);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
