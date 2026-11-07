using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary><see cref="IStudentStore"/> on EF Core; students are always loaded with all their enrollments.</summary>
public class StudentStore(AppDbContext db) : IStudentStore
{
    public async Task<IReadOnlyList<Student>> FindByExternalIdsAsync(
        SubmissionSource source, IReadOnlyCollection<string> externalUserIds, CancellationToken cancellationToken = default) =>
        await db.Students
            .Include(s => s.Enrollments)
            .Where(s => s.Source == source && externalUserIds.Contains(s.ExternalUserId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Student>> GetEnrolledAsync(
        SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
        await db.Students
            .Include(s => s.Enrollments)
            .Where(s => s.Source == source && s.Enrollments.Any(e => e.CourseId == courseId && e.Active))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Student>> GetByCourseAsync(
        SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
        await db.Students
            .Include(s => s.Enrollments)
            .Where(s => s.Source == source && s.Enrollments.Any(e => e.CourseId == courseId))
            .ToListAsync(cancellationToken);

    public void Add(Student student) => db.Students.Add(student);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
