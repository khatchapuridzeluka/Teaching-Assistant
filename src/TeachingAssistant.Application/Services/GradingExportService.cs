using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class GradingExportService : IGradingExportService
{
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly ISubmissionStore _submissions;
    private readonly ICourseAssignmentStore _assignments;
    private readonly IGradingDocumentRenderer _renderer;
    private readonly TimeProvider _timeProvider;

    public GradingExportService(
        IGradingStore gradings, IGradingReportStore reports, ISubmissionStore submissions, ICourseAssignmentStore assignments,
        IGradingDocumentRenderer renderer, TimeProvider timeProvider)
    {
        _gradings = gradings;
        _reports = reports;
        _submissions = submissions;
        _assignments = assignments;
        _renderer = renderer;
        _timeProvider = timeProvider;
    }

    public async Task<ExportedFile?> ExportPdfAsync(Guid gradingId, CancellationToken cancellationToken = default)
    {
        var document = await BuildAsync(gradingId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        return new ExportedFile
        {
            Content = _renderer.RenderPdf(document),
            ContentType = "application/pdf",
            FileName = FileName(document),
        };
    }

    internal async Task<GradingDocument?> BuildAsync(Guid gradingId, CancellationToken cancellationToken)
    {
        var grading = await _gradings.FindAsync(gradingId, cancellationToken);
        if (grading is null)
        {
            return null;
        }

        var submission = await _submissions.FindByVersionAsync(grading.VersionId, cancellationToken);
        if (submission is null)
        {
            return null;
        }

        var version = submission.Versions.Single(v => v.Id == grading.VersionId);
        var report = await _reports.FindByGradingAsync(grading.Id, cancellationToken);
        var assignment = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, submission.CourseId, cancellationToken))
            .FirstOrDefault(a => a.ExternalAssignmentId == submission.AssignmentId);

        var status = grading.Status == GradingStatus.Discarded
            ? GradingDocumentStatus.Discarded
            : report is null
                ? GradingDocumentStatus.Draft
                : report.Status == ReportStatus.Approved
                    ? GradingDocumentStatus.Approved
                    : GradingDocumentStatus.InReview;

        var mistakes = report is not null
            ? report.Mistakes
                .Where(m => m.Decision != MistakeDecision.Rejected)
                .OrderBy(m => m.Position)
                .Select(m => new GradingDocumentMistake
                {
                    Severity = m.Severity,
                    Category = m.Category,
                    Message = m.Message,
                    Points = m.Points,
                    File = m.File,
                    StartLine = m.StartLine,
                    EndLine = m.EndLine,
                })
                .ToList()
            : grading.Mistakes
                .OrderBy(m => m.Position)
                .Select(m => new GradingDocumentMistake
                {
                    Severity = m.Severity,
                    Category = m.Category,
                    Message = m.Message,
                    Points = m.Points,
                    File = m.File,
                    StartLine = m.StartLine,
                    EndLine = m.EndLine,
                })
                .ToList();

        var strengths = report is not null
            ? report.Strengths
                .Where(s => s.Decision != MistakeDecision.Rejected)
                .OrderBy(s => s.Position)
                .Select(s => new GradingDocumentStrength
                {
                    Category = s.Category,
                    Message = s.Message,
                    File = s.File,
                    StartLine = s.StartLine,
                    EndLine = s.EndLine,
                })
                .ToList()
            : grading.Strengths
                .OrderBy(s => s.Position)
                .Select(s => new GradingDocumentStrength
                {
                    Category = s.Category,
                    Message = s.Message,
                    File = s.File,
                    StartLine = s.StartLine,
                    EndLine = s.EndLine,
                })
                .ToList();

        return new GradingDocument
        {
            StudentName = submission.StudentName,
            AssignmentTitle = assignment?.Title ?? "Assignment",
            DueAt = assignment?.DueAt,
            VersionNumber = version.Number,
            TurnedInAt = version.TurnedInAt,
            Status = status,
            Score = report?.Score ?? grading.Score,
            MaxScore = grading.MaxScore ?? assignment?.MaxPoints,
            Bonus = report is null ? grading.Bonus : report.Bonus,
            AiScore = grading.Score,
            AiBonus = grading.Bonus,
            Review = report?.Review ?? grading.Review,
            Mistakes = mistakes,
            Strengths = strengths,
            Model = grading.Model,
            GradedAt = grading.CreatedAt,
            ApprovedAt = report?.ApprovedAt,
            GeneratedAt = _timeProvider.GetUtcNow(),
        };
    }

    internal static string FileName(GradingDocument document)
    {
        var name = $"{document.AssignmentTitle} - {document.StudentName} - v{document.VersionNumber}";
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new StringBuilder();
        foreach (var c in name)
        {
            safe.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        return $"{safe.ToString().Trim()}.pdf";
    }
}
