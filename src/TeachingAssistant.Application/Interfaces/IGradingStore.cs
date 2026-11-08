using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>Our saved AI gradings, always loaded with their mistakes.</summary>
public interface IGradingStore
{
    /// <summary>Every grading of these versions (drafts and discarded), newest first.</summary>
    Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default);

    Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every grading (drafts and discarded) of a course's stored versions as list rows, newest first. A grading reaches its
    /// course, assignment and student through its version's submission. With assignmentId, only that assignment's
    /// gradings; with studentId (the source's user ID), only that student's. Both can be combined.
    /// </summary>
    Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId,
        CancellationToken cancellationToken = default);

    void Add(AiGrading grading);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
