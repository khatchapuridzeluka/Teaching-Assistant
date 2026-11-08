using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class GradingDraftService : IGradingDraftService
{
    private readonly IGradingStore _gradings;
    private readonly TimeProvider _timeProvider;

    public GradingDraftService(IGradingStore gradings, TimeProvider timeProvider)
    {
        _gradings = gradings;
        _timeProvider = timeProvider;
    }

    public async Task<GradingResponse?> GetCurrentAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var gradings = await _gradings.GetByVersionsAsync([versionId], cancellationToken);
        var current = gradings.FirstOrDefault(g => g.Status == GradingStatus.Draft);
        return current is null ? null : ToResponse(current);
    }

    public async Task<IReadOnlyList<GradingResponse>> GetHistoryAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var gradings = await _gradings.GetByVersionsAsync([versionId], cancellationToken);
        return gradings.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AssignmentGradingResponse>> GetByCourseAsync(
        string courseId, string? assignmentId, string? studentId, bool includeAll, CancellationToken cancellationToken = default)
    {
        var rows = await _gradings.GetSummariesAsync(SubmissionSource.GoogleClassroom, courseId, assignmentId, studentId, cancellationToken);
        var current = rows
            .Where(r => r.Status == GradingStatus.Draft)
            .GroupBy(r => r.VersionId)
            .Select(group => group.MaxBy(r => r.CreatedAt)!.Id)
            .ToHashSet();

        return rows
            .Where(r => includeAll || current.Contains(r.Id))
            .Select(r => new AssignmentGradingResponse(r.Id, r.VersionId, r.VersionNumber, r.AssignmentId, r.SubmissionId,
                r.StudentId, r.StudentName, r.Status, current.Contains(r.Id), r.Score, r.MaxScore, r.Bonus, r.Major, r.Minor, r.Style,
                r.Model, r.CreatedAt))
            .ToList();
    }

    public async Task<GradingResponse?> DiscardAsync(Guid gradingId, CancellationToken cancellationToken = default)
    {
        var grading = await _gradings.FindAsync(gradingId, cancellationToken);
        if (grading is null)
        {
            return null;
        }

        grading.Discard(_timeProvider.GetUtcNow());
        await _gradings.SaveChangesAsync(cancellationToken);
        return ToResponse(grading);
    }

    internal static GradingResponse ToResponse(AiGrading g) => new(
        g.Id, g.VersionId, g.Status, g.Model, g.Review, g.Score, g.MaxScore, g.Bonus,
        g.Mistakes.OrderBy(m => m.Position)
            .Select(m => new CodeMistake(m.File, m.StartLine, m.EndLine, m.Severity, m.Category, m.Message)).ToList(),
        g.InstructionsVersion, g.SentFiles, g.SkippedFiles, g.CreatedAt);
}
