using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeGradingStore : IGradingStore
{
    public List<GradingSummary> Summaries { get; } = [];

    public List<AiGrading> Gradings { get; } = [];

    public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GradingSummary>>(Summaries
            .Where(s => (assignmentId is null || s.AssignmentId == assignmentId) && (studentId is null || s.StudentId == studentId))
            .ToList());

    public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Gradings.FirstOrDefault(g => g.Id == gradingId));

    public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiGrading>>(Gradings.Where(g => versionIds.Contains(g.VersionId)).ToList());

    public Task<IReadOnlyList<DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Add(AiGrading grading) => Gradings.Add(grading);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public GradingSummary AddDraft(AiGrading grading, string assignmentId, string submissionId, string studentId, int versionNumber,
        ReportStatus? reportStatus = null)
    {
        Gradings.Add(grading);
        var summary = new GradingSummary
        {
            Id = grading.Id,
            VersionId = grading.VersionId,
            VersionNumber = versionNumber,
            AssignmentId = assignmentId,
            SubmissionId = submissionId,
            StudentId = studentId,
            StudentName = "Nino",
            Status = grading.Status,
            Score = grading.Score,
            MaxScore = grading.MaxScore,
            Major = 0,
            Minor = 0,
            Style = 0,
            Model = grading.Model,
            CreatedAt = grading.CreatedAt,
            ReportStatus = reportStatus,
        };
        Summaries.Add(summary);
        return summary;
    }
}
