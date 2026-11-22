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
        var current = (await _gradings.GetSummariesAsync(SubmissionSource.GoogleClassroom, courseId, null, studentId, cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .GroupBy(g => g.AssignmentId)
            .Select(g => Current(g.ToList()))
            .ToList();

        var mistakes = new List<StudentMistake>();
        var strengths = new List<StudentStrength>();
        var assignments = new Dictionary<string, StudentRecordAssignment>();
        foreach (var summary in current)
        {
            var submission = await _submissions.FindByVersionAsync(summary.VersionId, cancellationToken);
            if (submission is null || await _gradings.FindAsync(summary.Id, cancellationToken) is not { } draft)
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

            assignments[submission.AssignmentId] = new StudentRecordAssignment
            {
                AssignmentId = submission.AssignmentId,
                Source = source,
                Score = new ReportScore
                {
                    ReportId = report?.Id ?? Guid.Empty,
                    Score = report?.Score,
                    Bonus = report?.Bonus,
                    AiScore = draft.Score,
                    AiBonus = draft.Bonus,
                    MaxScore = draft.MaxScore,
                },
            };
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
            assignments[assignmentId] = new StudentRecordAssignment
            {
                AssignmentId = assignmentId,
                Source = StudentRecordSource.Approved,
                Score = scores.GetValueOrDefault(reportId),
            };
        }

        return new StudentRecords { Mistakes = mistakes, Strengths = strengths, Assignments = assignments };
    }

    /// <summary>
    /// Picks the grading an assignment's records come from: the newest one with a report (the teacher reviewed it, so it wins
    /// over any AI draft, even a newer one or one of a newer version), otherwise the newest AI draft.
    /// </summary>
    internal static GradingSummary Current(IReadOnlyList<GradingSummary> gradings)
    {
        var newestFirst = gradings
            .OrderByDescending(g => g.VersionNumber)
            .ThenByDescending(g => g.CreatedAt)
            .ToList();
        return newestFirst.FirstOrDefault(g => g.ReportStatus is not null) ?? newestFirst[0];
    }
}
