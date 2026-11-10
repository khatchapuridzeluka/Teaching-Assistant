using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Jobs;

internal static class JobGroups
{
    public static void Track(Job group, IReadOnlyList<Job> children, DateTimeOffset now)
    {
        var finished = children.Count(c => c.IsFinished);
        var failed = children.Count(c => c.Status == JobStatus.Failed);
        var result = JobPayloads.Write(new GradeAssignmentResult(
            children.Count(c => c.Status == JobStatus.Succeeded), failed, children.Count(c => c.Status == JobStatus.Canceled)));
        group.TrackChildren(Math.Max(group.ProgressTotal ?? 0, children.Count), finished, failed, result, now);
    }
}
