using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence;

public class CourseGuidanceStore(AppDbContext db) : ICourseGuidanceStore
{
    public Task<CourseGuidance?> FindAsync(string courseId, CancellationToken cancellationToken = default)
    {
        return db.CourseGuidance.FirstOrDefaultAsync(g => g.CourseId == courseId, cancellationToken);
    }

    public void Add(CourseGuidance guidance)
    {
        db.CourseGuidance.Add(guidance);
    }

    public void Remove(CourseGuidance guidance)
    {
        db.CourseGuidance.Remove(guidance);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
