using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class GradingDraftService : IGradingDraftService
{
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly IStudentMistakeStore _studentMistakes;
    private readonly IStudentStrengthStore _studentStrengths;
    private readonly TimeProvider _timeProvider;

    public GradingDraftService(
        IGradingStore gradings, IGradingReportStore reports, IStudentMistakeStore studentMistakes,
        IStudentStrengthStore studentStrengths, TimeProvider timeProvider)
    {
        _gradings = gradings;
        _reports = reports;
        _studentMistakes = studentMistakes;
        _studentStrengths = studentStrengths;
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
            .Select(r => new AssignmentGradingResponse
            {
                Id = r.Id,
                VersionId = r.VersionId,
                VersionNumber = r.VersionNumber,
                AssignmentId = r.AssignmentId,
                SubmissionId = r.SubmissionId,
                StudentId = r.StudentId,
                StudentName = r.StudentName,
                Status = r.Status,
                Current = current.Contains(r.Id),
                Score = r.Score,
                MaxScore = r.MaxScore,
                Bonus = r.Bonus,
                Major = r.Major,
                Minor = r.Minor,
                Style = r.Style,
                Model = r.Model,
                CreatedAt = r.CreatedAt,
                ReportStatus = r.ReportStatus,
            })
            .ToList();
    }

    public async Task<GradingResponse?> DiscardAsync(
        Guid gradingId, DiscardGradingRequest? request = null, CancellationToken cancellationToken = default)
    {
        if (request?.Reason == DiscardReason.Superseded)
        {
            throw new InvalidRequestException("Superseded is set automatically when another draft is approved; choose another reason.");
        }

        if (request?.Note is { Length: > AiGrading.MaxDiscardNoteLength })
        {
            throw new InvalidRequestException($"The note can have at most {AiGrading.MaxDiscardNoteLength} characters.");
        }

        var grading = await _gradings.FindAsync(gradingId, cancellationToken);
        if (grading is null)
        {
            return null;
        }

        grading.Discard(_timeProvider.GetUtcNow(), request?.Reason, request?.Note);
        if (await _reports.FindByGradingAsync(gradingId, cancellationToken) is { } report)
        {
            _studentMistakes.RemoveRange(await _studentMistakes.GetByReportAsync(report.Id, cancellationToken));
            _studentStrengths.RemoveRange(await _studentStrengths.GetByReportAsync(report.Id, cancellationToken));
        }

        await _gradings.SaveChangesAsync(cancellationToken);
        return ToResponse(grading);
    }

    internal static GradingResponse ToResponse(AiGrading g) => new()
    {
        Id = g.Id,
        VersionId = g.VersionId,
        Status = g.Status,
        Model = g.Model,
        Review = g.Review,
        Score = g.Score,
        MaxScore = g.MaxScore,
        Bonus = g.Bonus,
        Mistakes = g.Mistakes.OrderBy(m => m.Position)
            .Select(m => new CodeMistake
            {
                File = m.File,
                StartLine = m.StartLine,
                EndLine = m.EndLine,
                Severity = m.Severity,
                Category = m.Category,
                Message = m.Message,
                Id = m.Id,
                Points = m.Points,
            }).ToList(),
        InstructionsVersion = g.InstructionsVersion,
        SentFiles = g.SentFiles,
        SkippedFiles = g.SkippedFiles,
        CreatedAt = g.CreatedAt,
        Strengths = g.Strengths.OrderBy(s => s.Position)
            .Select(s => new CodeStrength
            {
                File = s.File,
                StartLine = s.StartLine,
                EndLine = s.EndLine,
                Category = s.Category,
                Message = s.Message,
                Id = s.Id,
            }).ToList(),
        BuildStatus = g.BuildStatus,
        BuildOutput = g.BuildOutput,
        StyleFindings = g.StyleFindings,
        StyleOutput = g.StyleOutput,
        PmdFindings = g.PmdFindings,
        PmdOutput = g.PmdOutput,
        DiscardReason = g.DiscardReason,
        DiscardNote = g.DiscardNote,
    };
}
