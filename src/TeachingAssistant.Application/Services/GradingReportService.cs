using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class GradingReportService : IGradingReportService
{
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentMistakeStore _studentMistakes;
    private readonly TimeProvider _timeProvider;

    public GradingReportService(
        IGradingStore gradings, IGradingReportStore reports, ISubmissionStore submissions, IStudentMistakeStore studentMistakes,
        TimeProvider timeProvider)
    {
        _gradings = gradings;
        _reports = reports;
        _submissions = submissions;
        _studentMistakes = studentMistakes;
        _timeProvider = timeProvider;
    }

    public async Task<GradingReportResponse?> GetAsync(Guid gradingId, CancellationToken cancellationToken = default)
    {
        var grading = await _gradings.FindAsync(gradingId, cancellationToken);
        if (grading is null)
        {
            return null;
        }

        var report = await _reports.FindByGradingAsync(gradingId, cancellationToken);
        return report is null ? null : ToResponse(report, grading);
    }

    public Task<GradingReportResponse?> SaveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default)
    {
        return SaveAsync(gradingId, request, approve: false, cancellationToken);
    }

    public Task<GradingReportResponse?> ApproveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default)
    {
        return SaveAsync(gradingId, request, approve: true, cancellationToken);
    }

    private async Task<GradingReportResponse?> SaveAsync(
        Guid gradingId, SaveGradingReportRequest request, bool approve, CancellationToken cancellationToken)
    {
        var grading = await _gradings.FindAsync(gradingId, cancellationToken);
        if (grading is null)
        {
            return null;
        }

        Validate(grading, request);

        var now = _timeProvider.GetUtcNow();
        var report = await _reports.FindByGradingAsync(gradingId, cancellationToken);
        var submission = report is null || approve
            ? await _submissions.FindByVersionAsync(grading.VersionId, cancellationToken)
              ?? throw new InvalidRequestException("The submission of this draft no longer exists.")
            : null;

        if (report is null)
        {
            report = new GradingReport(grading, submission!, now);
            _reports.Add(report);
        }

        var edits = request.Mistakes.Select(m => new ReportMistakeEdit(m.SourceMistakeId, m.Rejected, m.Severity, m.Category, m.Message));
        report.Save(grading, request.Score, request.Bonus, request.Review.Trim(), edits, now);
        _studentMistakes.RemoveRange(await _studentMistakes.GetByReportAsync(report.Id, cancellationToken));

        if (approve)
        {
            report.Approve(now);
            _studentMistakes.AddRange(StudentMistake.FromApprovedReport(report, submission!));
        }

        await _reports.SaveChangesAsync(cancellationToken);
        return ToResponse(report, grading);
    }

    private static void Validate(AiGrading grading, SaveGradingReportRequest request)
    {
        if (grading.Status == GradingStatus.Discarded)
        {
            throw new InvalidRequestException("This draft was discarded; grade again or pick another draft to review.");
        }

        if (request.Score < 0 || request.Bonus < 0)
        {
            throw new InvalidRequestException("Score and bonus cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.Review))
        {
            throw new InvalidRequestException("The review text cannot be empty.");
        }

        var known = grading.Mistakes.Select(m => m.Id).ToHashSet();
        var unknown = request.Mistakes.FirstOrDefault(m => !known.Contains(m.SourceMistakeId));
        if (unknown is not null)
        {
            throw new InvalidRequestException($"Mistake {unknown.SourceMistakeId} is not one of this draft's mistakes.");
        }

        var empty = request.Mistakes.FirstOrDefault(m => !m.Rejected && string.IsNullOrWhiteSpace(m.Message));
        if (empty is not null)
        {
            throw new InvalidRequestException("A kept mistake needs a message; reject it instead of leaving it empty.");
        }
    }

    private static GradingReportResponse ToResponse(GradingReport report, AiGrading grading) => new(
        report.Id, report.GradingId, report.VersionId, report.StudentId, report.CourseId, report.AssignmentId, report.Status, report.Score, grading.MaxScore, report.Bonus, report.Review,
        report.Mistakes.OrderBy(m => m.Position)
            .Select(m => new ReportMistakeResponse(m.Id, m.SourceMistakeId, m.File, m.StartLine, m.EndLine, m.Severity, m.Category,
                m.Message, m.Decision))
            .ToList(),
        report.CreatedAt, report.UpdatedAt, report.ApprovedAt);
}
