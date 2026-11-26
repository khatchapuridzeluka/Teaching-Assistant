using System.ComponentModel;
using System.Globalization;
using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Tools;

public class ResultTools
{
    internal const int MaxReviewLength = 4_000;

    private readonly IGradingStore _gradings;
    private readonly IGradingReportService _reports;
    private readonly IMistakeAnalysisStore _analyses;
    private readonly AgentRunContext _context;

    public ResultTools(IGradingStore gradings, IGradingReportService reports, IMistakeAnalysisStore analyses, AgentRunContext context)
    {
        _gradings = gradings;
        _reports = reports;
        _analyses = analyses;
        _context = context;
    }

    [AgentTool("get_grading")]
    [Description(
        "Reads one student's current grading of one assignment: the teacher's report when one was saved (approved or still in "
        + "review), otherwise the AI draft, clearly marked as not reviewed. It returns the score, the review, every mistake "
        + "(severity, category, points, file and lines, message) and the strengths, without the student's name. It is free and "
        + "makes no AI call. Use it to answer what a student got, why they lost points or what they did well; when the student "
        + "has no current draft, say so and offer grade_student_assignment.")]
    public async Task<string> GetGradingAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("The student id, as returned by list_submissions or find_students")] string studentId,
        CancellationToken cancellationToken = default)
    {
        var current = (await _gradings.GetSummariesAsync(SubmissionSource.GoogleClassroom, courseId, assignmentId, studentId, cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .OrderByDescending(g => g.VersionNumber)
            .ThenByDescending(g => g.CreatedAt)
            .FirstOrDefault();
        if (current is null || await _gradings.FindAsync(current.Id, cancellationToken) is not { } grading)
        {
            return "This student has no current grading for this assignment (never graded, or every draft was discarded). "
                + "grade_student_assignment grades it (one paid grading); ask the teacher first.";
        }

        _context.RecordLinks([new AgentLink
        {
            Kind = AgentLinkKind.Submission,
            Label = $"The grading · {current.StudentName}",
            CourseId = courseId,
            AssignmentId = assignmentId,
            SubmissionId = current.SubmissionId,
            StudentId = studentId,
        }]);

        var report = await _reports.GetAsync(grading.Id, cancellationToken);
        return report is null ? DescribeDraft(grading, current.VersionNumber) : DescribeReport(report, grading, current.VersionNumber);
    }

    [AgentTool("get_common_mistakes")]
    [Description(
        "Reads the latest saved Common mistakes analysis of one assignment: the class overview, the groups of similar mistakes "
        + "with how many students and mistakes each has, and the suggestion for the next lecture. It is free and makes no AI "
        + "call. Use it when the teacher asks about the class's common mistakes; only when there is none, or the teacher asks "
        + "for a fresh one, start analyze_common_mistakes, which is paid.")]
    public async Task<string> GetCommonMistakesAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (await _analyses.GetLatestAsync(courseId, assignmentId, cancellationToken) is not { } analysis)
        {
            return "No Common mistakes analysis of this assignment is saved yet. analyze_common_mistakes writes one (one paid AI "
                + "call over the current drafts); ask the teacher first.";
        }

        _context.RecordLinks([new AgentLink
        {
            Kind = AgentLinkKind.CommonMistakes,
            Label = "Common mistakes",
            CourseId = courseId,
            AssignmentId = assignmentId,
        }]);
        var made = analysis.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return $"The latest Common mistakes analysis, made {made} UTC from the drafts that existed then (gradings since are not "
            + $"in it): {JobReporter.DescribeAnalysis(analysis)}";
    }

    private static string DescribeDraft(AiGrading grading, int versionNumber)
    {
        var text = new StringBuilder();
        text.Append($"AI draft of version {versionNumber}, not reviewed by the teacher yet (model {grading.Model}). ");
        text.AppendLine(Score(grading.Score, grading.MaxScore, grading.Bonus));
        text.AppendLine().AppendLine("Review:").AppendLine(Shorten(grading.Review));
        text.AppendLine().AppendLine(grading.Mistakes.Count == 0 ? "Mistakes: none." : "Mistakes:");
        foreach (var mistake in grading.Mistakes.OrderBy(m => m.Position))
        {
            text.AppendLine(Mistake(mistake.Severity, mistake.Category, mistake.Points, mistake.File, mistake.StartLine, mistake.EndLine, mistake.Message));
        }

        text.AppendLine().AppendLine(grading.Strengths.Count == 0 ? "Strengths: none listed." : "Strengths:");
        foreach (var strength in grading.Strengths.OrderBy(s => s.Position))
        {
            text.AppendLine(Strength(strength.Category, strength.File, strength.StartLine, strength.EndLine, strength.Message));
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd();
    }

    private static string DescribeReport(GradingReportResponse report, AiGrading grading, int versionNumber)
    {
        var state = report.Status == ReportStatus.Approved ? "approved by the teacher" : "saved by the teacher, still in review";
        var kept = report.Mistakes.Where(m => m.Decision != MistakeDecision.Rejected).ToList();
        var rejected = report.Mistakes.Count - kept.Count;
        var text = new StringBuilder();
        text.Append($"The teacher's report of version {versionNumber}, {state}. ");
        text.Append(Score(report.Score, report.MaxScore, report.Bonus));
        if (grading.Score is { } aiScore && aiScore != report.Score)
        {
            text.Append($" The AI draft had {aiScore.ToString(CultureInfo.InvariantCulture)}.");
        }

        text.AppendLine();
        text.AppendLine().AppendLine("Review:").AppendLine(Shorten(report.Review));
        text.AppendLine().AppendLine(kept.Count == 0 ? "Mistakes: none." : "Mistakes:");
        foreach (var mistake in kept)
        {
            var edited = mistake.Decision == MistakeDecision.Edited ? " (edited by the teacher)" : "";
            text.AppendLine(Mistake(mistake.Severity, mistake.Category, mistake.Points, mistake.File, mistake.StartLine, mistake.EndLine, mistake.Message) + edited);
        }

        if (rejected > 0)
        {
            text.AppendLine($"The teacher rejected {rejected} of the AI's mistakes; they are not part of the grade.");
        }

        var strengths = report.Strengths.Where(s => s.Decision != MistakeDecision.Rejected).ToList();
        text.AppendLine().AppendLine(strengths.Count == 0 ? "Strengths: none listed." : "Strengths:");
        foreach (var strength in strengths)
        {
            text.AppendLine(Strength(strength.Category, strength.File, strength.StartLine, strength.EndLine, strength.Message));
        }

        return text.ToString().ReplaceLineEndings("\n").TrimEnd();
    }

    private static string Score(double? score, double? maxScore, double? bonus)
    {
        if (score is not { } value)
        {
            return "No score.";
        }

        var max = maxScore is { } m ? $" / {m.ToString(CultureInfo.InvariantCulture)}" : "";
        var extra = bonus is { } b and > 0 ? $", bonus {b.ToString(CultureInfo.InvariantCulture)}" : "";
        return $"Score {value.ToString(CultureInfo.InvariantCulture)}{max}{extra}.";
    }

    private static string Mistake(
        MistakeSeverity severity, MistakeCategory category, double? points, string? file, int? startLine, int? endLine, string message)
    {
        var cost = points is { } p ? $", -{p.ToString(CultureInfo.InvariantCulture)} points" : "";
        return $"- {severity} {category}{cost}{Where(file, startLine, endLine)}: {message}";
    }

    private static string Strength(MistakeCategory category, string? file, int? startLine, int? endLine, string message)
    {
        return $"- {category}{Where(file, startLine, endLine)}: {message}";
    }

    private static string Where(string? file, int? startLine, int? endLine)
    {
        if (file is null)
        {
            return "";
        }

        var lines = startLine is { } start ? endLine is { } end && end != start ? $":{start}-{end}" : $":{start}" : "";
        return $" ({file}{lines})";
    }

    private static string Shorten(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= MaxReviewLength ? trimmed : trimmed[..MaxReviewLength] + "…";
    }
}
