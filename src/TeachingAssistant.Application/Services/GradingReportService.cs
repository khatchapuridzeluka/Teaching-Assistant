using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class GradingReportService : IGradingReportService
{
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentMistakeStore _studentMistakes;
    private readonly IStudentStrengthStore _studentStrengths;
    private readonly TimeProvider _timeProvider;

    public GradingReportService(
        IGradingStore gradings, IGradingReportStore reports, ISubmissionStore submissions, IStudentMistakeStore studentMistakes,
        IStudentStrengthStore studentStrengths, TimeProvider timeProvider)
    {
        _gradings = gradings;
        _reports = reports;
        _submissions = submissions;
        _studentMistakes = studentMistakes;
        _studentStrengths = studentStrengths;
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

        var edits = request.Mistakes.Select(m => new ReportMistakeEdit
        {
            SourceMistakeId = m.SourceMistakeId,
            Rejected = m.Rejected,
            Severity = m.Severity,
            Category = m.Category,
            Message = m.Message,
            Points = m.Points,
        });
        var strengthEdits = (request.Strengths ?? [])
            .Select(s => new ReportStrengthEdit
            {
                SourceStrengthId = s.SourceStrengthId,
                Rejected = s.Rejected,
                Category = s.Category,
                Message = s.Message,
            });
        report.Save(grading, request.Score, request.Bonus, request.Review.Trim(), edits, now, strengthEdits);
        await RemoveRecordsAsync(report.Id, cancellationToken);

        if (approve)
        {
            report.Approve(now);
            _studentMistakes.AddRange(StudentMistake.FromApprovedReport(report, submission!));
            _studentStrengths.AddRange(StudentStrength.FromApprovedReport(report, submission!));
            await DiscardOtherDraftsAsync(grading, submission!, now, cancellationToken);
        }

        await _reports.SaveChangesAsync(cancellationToken);
        return ToResponse(report, grading);
    }

    private async Task DiscardOtherDraftsAsync(
        AiGrading approved, Submission submission, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var others = (await _gradings.GetByVersionsAsync(submission.Versions.Select(v => v.Id).ToList(), cancellationToken))
            .Where(g => g.Id != approved.Id && g.Status == GradingStatus.Draft)
            .ToList();

        foreach (var other in others)
        {
            other.Discard(now, DiscardReason.Superseded);
            if (await _reports.FindByGradingAsync(other.Id, cancellationToken) is { } otherReport)
            {
                await RemoveRecordsAsync(otherReport.Id, cancellationToken);
            }
        }
    }

    private async Task RemoveRecordsAsync(Guid reportId, CancellationToken cancellationToken)
    {
        _studentMistakes.RemoveRange(await _studentMistakes.GetByReportAsync(reportId, cancellationToken));
        _studentStrengths.RemoveRange(await _studentStrengths.GetByReportAsync(reportId, cancellationToken));
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

        if (request.Mistakes.Any(m => m.Points < 0))
        {
            throw new InvalidRequestException("A mistake cannot deduct a negative number of points.");
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

        var strengths = request.Strengths ?? [];
        var knownStrengths = grading.Strengths.Select(s => s.Id).ToHashSet();
        var unknownStrength = strengths.FirstOrDefault(s => !knownStrengths.Contains(s.SourceStrengthId));
        if (unknownStrength is not null)
        {
            throw new InvalidRequestException($"Strength {unknownStrength.SourceStrengthId} is not one of this draft's strengths.");
        }

        if (strengths.Any(s => !s.Rejected && string.IsNullOrWhiteSpace(s.Message)))
        {
            throw new InvalidRequestException("A kept strength needs a message; reject it instead of leaving it empty.");
        }
    }

    private static GradingReportResponse ToResponse(GradingReport report, AiGrading grading) => new()
    {
        Id = report.Id,
        GradingId = report.GradingId,
        VersionId = report.VersionId,
        StudentId = report.StudentId,
        CourseId = report.CourseId,
        AssignmentId = report.AssignmentId,
        Status = report.Status,
        Score = report.Score,
        MaxScore = grading.MaxScore,
        Bonus = report.Bonus,
        Review = report.Review,
        Mistakes = report.Mistakes.OrderBy(m => m.Position)
            .Select(m => new ReportMistakeResponse
            {
                Id = m.Id,
                SourceMistakeId = m.SourceMistakeId,
                File = m.File,
                StartLine = m.StartLine,
                EndLine = m.EndLine,
                Severity = m.Severity,
                Category = m.Category,
                Message = m.Message,
                Points = m.Points,
                Decision = m.Decision,
            })
            .ToList(),
        Strengths = report.Strengths.OrderBy(s => s.Position)
            .Select(s => new ReportStrengthResponse
            {
                Id = s.Id,
                SourceStrengthId = s.SourceStrengthId,
                File = s.File,
                StartLine = s.StartLine,
                EndLine = s.EndLine,
                Category = s.Category,
                Message = s.Message,
                Decision = s.Decision,
            })
            .ToList(),
        CreatedAt = report.CreatedAt,
        UpdatedAt = report.UpdatedAt,
        ApprovedAt = report.ApprovedAt,
    };
}
