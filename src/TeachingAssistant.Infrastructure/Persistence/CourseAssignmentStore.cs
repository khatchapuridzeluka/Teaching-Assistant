using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary><see cref="ICourseAssignmentStore"/> on EF Core.</summary>
public class CourseAssignmentStore(AppDbContext db) : ICourseAssignmentStore
{
    public async Task<IReadOnlyList<CourseAssignment>> GetByCourseAsync(
        SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
        await db.CourseAssignments
            .Where(a => a.Source == source && a.CourseId == courseId)
            .ToListAsync(cancellationToken);

    public void Add(CourseAssignment assignment) => db.CourseAssignments.Add(assignment);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
