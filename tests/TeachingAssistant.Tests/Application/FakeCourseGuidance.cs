using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeCourseGuidance : ICourseGuidanceStore
{
    public List<CourseGuidance> All { get; } = [];

    public Task<CourseGuidance?> FindAsync(string courseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(g => g.CourseId == courseId));

    public void Add(CourseGuidance guidance) => All.Add(guidance);

    public void Remove(CourseGuidance guidance) => All.Remove(guidance);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
