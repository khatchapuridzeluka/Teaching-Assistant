using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentRecordCollector
{
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentMistakeStore _mistakes;
    private readonly IStudentStrengthStore _strengths;

    public StudentRecordCollector(
        IGradingStore gradings, IGradingReportStore reports, ISubmissionStore submissions, IStudentMistakeStore mistakes,
        IStudentStrengthStore strengths)
    {
        _gradings = gradings;
        _reports = reports;
        _submissions = submissions;
        _mistakes = mistakes;
        _strengths = strengths;
    }

    public async Task<StudentRecords> CollectAsync(string courseId, string studentId, CancellationToken cancellationToken = default)
    {
        var latestDrafts = (await _gradings.GetSummariesAsync(SubmissionSource.GoogleClassroom, courseId, null, studentId, cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .GroupBy(g => g.AssignmentId)
            .Select(g => g.OrderByDescending(d => d.VersionNumber).ThenByDescending(d => d.CreatedAt).First())
            .ToList();

        var mistakes = new List<StudentMistake>();
        var strengths = new List<StudentStrength>();
        var assignments = new Dictionary<string, StudentRecordAssignment>();
        foreach (var summary in latestDrafts)
        {
            var submission = await _submissions.FindByVersionAsync(summary.VersionId, cancellationToken);
            if (submission is null || submission.LatestVersion?.Id != summary.VersionId
                || await _gradings.FindAsync(summary.Id, cancellationToken) is not { } draft)
            {
                continue;
            }

            var report = await _reports.FindByGradingAsync(draft.Id, cancellationToken);
            StudentRecordSource source;
            if (report is not null)
            {
                mistakes.AddRange(StudentMistake.FromReport(report, submission));
                strengths.AddRange(StudentStrength.FromReport(report, submission));
                source = report.Status == ReportStatus.Approved ? StudentRecordSource.Approved : StudentRecordSource.InReview;
            }
            else
            {
                mistakes.AddRange(StudentMistake.FromDraft(draft, submission));
                strengths.AddRange(StudentStrength.FromDraft(draft, submission));
                source = StudentRecordSource.Draft;
            }

            assignments[submission.AssignmentId] = new StudentRecordAssignment(submission.AssignmentId, source,
                new ReportScore(report?.Id ?? Guid.Empty, report?.Score, report?.Bonus, draft.Score, draft.Bonus, draft.MaxScore));
        }

        var approvedMistakes = (await _mistakes.GetByStudentAsync(courseId, studentId, cancellationToken))
            .Where(m => !assignments.ContainsKey(m.AssignmentId))
            .ToList();
        var approvedStrengths = (await _strengths.GetByStudentAsync(courseId, studentId, cancellationToken))
            .Where(s => !assignments.ContainsKey(s.AssignmentId))
            .ToList();
        mistakes.AddRange(approvedMistakes);
        strengths.AddRange(approvedStrengths);

        var approvedReports = approvedMistakes.Select(m => (m.AssignmentId, m.ReportId, m.ApprovedAt))
            .Concat(approvedStrengths.Select(s => (s.AssignmentId, s.ReportId, s.ApprovedAt)))
            .GroupBy(r => r.AssignmentId)
            .ToDictionary(g => g.Key, g => g.MaxBy(r => r.ApprovedAt).ReportId);
        var scores = (await _reports.GetScoresAsync(approvedReports.Values.Distinct().ToList(), cancellationToken))
            .ToDictionary(s => s.ReportId);
        foreach (var (assignmentId, reportId) in approvedReports)
        {
            assignments[assignmentId] = new StudentRecordAssignment(assignmentId, StudentRecordSource.Approved, scores.GetValueOrDefault(reportId));
        }

        return new StudentRecords(mistakes, strengths, assignments);
    }
}
