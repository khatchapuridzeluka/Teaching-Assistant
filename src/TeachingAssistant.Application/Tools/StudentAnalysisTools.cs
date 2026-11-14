using System.ComponentModel;
using System.Globalization;
using System.Text;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Tools;

public class StudentAnalysisTools
{
    private readonly IStudentAnalysisService _analyses;
    private readonly IJobService _jobService;
    private readonly AgentRunContext _context;
    private readonly TimeProvider _timeProvider;

    public StudentAnalysisTools(IStudentAnalysisService analyses, IJobService jobService, AgentRunContext context, TimeProvider timeProvider)
    {
        _analyses = analyses;
        _jobService = jobService;
        _context = context;
        _timeProvider = timeProvider;
    }

    [AgentTool("analyze_student")]
    [Description(
        "Gives the AI analysis of one student across all their graded assignments (per assignment the teacher's newest report "
        + "where they reviewed it, otherwise the newest AI draft; the analysis never gets the student's name). kind picks which: "
        + "Mistakes is feedback on the student's mistakes (recurring problems vs. one-off slips, where the points were lost) and "
        + "what to work on next; Strengths groups the student's strengths into skills with an overview of what they are good at; "
        + "Overview is the whole picture from the scores, mistakes and strengths together (level, direction, what holds them back) "
        + "and what to work on next. When a saved analysis of that kind is still current it is returned at once, free. Otherwise "
        + "one paid AI call is queued in the background and a job id is returned; wait for it to get the result. Use it when the "
        + "teacher asks how a student is doing, for feedback on a student, or for their mistakes, strengths or overview.")]
    public async Task<string> AnalyzeStudentAsync(
        [Description("The id of the course")] string courseId,
        [Description("The student id, as returned by find_students")] string studentId,
        [Description("Which analysis: Mistakes, Strengths or Overview")] string kind,
        [Description("true writes a new analysis (a paid AI call) even when the saved one is still current; set it only when the "
            + "teacher explicitly asks to analyze again")]
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<StudentAnalysisKind>(kind, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            return $"Unknown kind '{kind}'. Use Mistakes, Strengths or Overview.";
        }

        var link = Link(parsed, "The student's", courseId, studentId);
        var latest = await _analyses.GetLatestAsync(courseId, studentId, parsed, cancellationToken);
        if (latest is { Outdated: false } && !refresh)
        {
            _context.RecordLinks([link]);
            return $"The saved analysis is still current (nothing new was started):\n{Describe(latest)}";
        }

        var started = _timeProvider.GetUtcNow();
        var job = await _jobService.QueueAnalyzeStudentAsync(courseId, studentId, parsed, cancellationToken);
        _context.RecordQueuedJob(job.Id);
        _context.RecordLinks([link]);
        if (job.CreatedAt < started)
        {
            return $"A {Name(parsed)} of this student is already in progress (job id {job.Id}); nothing new was started. Wait for it "
                + "if you need its result.";
        }

        var why = latest is null ? " There was no saved one yet." : refresh ? "" : " The saved one was out of date: reports changed since.";
        return $"{Capitalized(Name(parsed))} queued (job id {job.Id}).{why} It is one paid AI call that runs in the background, "
            + "usually within a minute; wait for it to get the result, which also appears on the student's page.";
    }

    internal static AgentLink Link(StudentAnalysisKind kind, string owner, string courseId, string studentId)
    {
        return kind switch
        {
            StudentAnalysisKind.Mistakes => new AgentLink(AgentLinkKind.StudentMistakes, $"{owner} mistakes", courseId, StudentId: studentId),
            StudentAnalysisKind.Strengths => new AgentLink(AgentLinkKind.StudentStrengths, $"{owner} strengths", courseId, StudentId: studentId),
            _ => new AgentLink(AgentLinkKind.StudentOverview, $"{owner} overview", courseId, StudentId: studentId),
        };
    }

    internal static string Name(StudentAnalysisKind kind)
    {
        return kind switch
        {
            StudentAnalysisKind.Mistakes => "mistakes analysis",
            StudentAnalysisKind.Strengths => "strengths analysis",
            _ => "overview analysis",
        };
    }

    /// <summary>
    /// The analysis as the agent reads it: what it covers, the text the model wrote and, for strengths, the skill groups.
    /// </summary>
    internal static string Describe(StudentAnalysisResponse analysis)
    {
        var what = analysis.Kind switch
        {
            StudentAnalysisKind.Mistakes => "mistakes",
            StudentAnalysisKind.Strengths => "strengths",
            _ => "mistakes and strengths",
        };
        var text = new StringBuilder()
            .Append(Capitalized(Name(analysis.Kind))).Append(" from ").Append(analysis.Records).Append(' ').Append(what).Append(" in ")
            .Append(analysis.Assignments).Append(analysis.Assignments == 1 ? " graded assignment" : " graded assignments").Append(", written ")
            .Append(analysis.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .AppendLine(analysis.Outdated ? " (out of date: reports changed since)" : "")
            .Append(analysis.Kind == StudentAnalysisKind.Strengths ? "Overview: " : "Summary: ").AppendLine(analysis.Summary);
        if (!string.IsNullOrWhiteSpace(analysis.NextSteps))
        {
            text.Append("What to work on next: ").AppendLine(analysis.NextSteps);
        }

        var skills = analysis.Groups.Where(g => !g.Ungrouped).ToList();
        if (skills.Count > 0)
        {
            text.AppendLine("Skills:");
            foreach (var group in skills)
            {
                text.Append("- ").Append(group.Title).Append(" (").Append(group.Count).Append(group.Count == 1 ? " strength" : " strengths")
                    .Append(" in ").Append(group.Assignments).Append(group.Assignments == 1 ? " assignment" : " assignments").Append("): ")
                    .AppendLine(group.Explanation);
            }
        }

        return text.ToString().TrimEnd();
    }

    private static string Capitalized(string text)
    {
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
