using System.Globalization;
using System.Text;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentAnalysisService : IStudentAnalysisService
{
    private const string MistakesPrompt =
        """
        You help a university programming teacher (Advanced Java, C++ basics) give one student feedback on the mistakes
        they make across the course. The student is called "the student" and is never named. You get every assignment
        the teacher has reviewed and approved for this student, in due-date order, with the points it was out of, the
        points its mistakes cost, and each mistake the teacher kept: its severity, category, the points it cost, whether
        it is new or repeated (the same category in an earlier assignment), and a short description. The trends at the
        end are counted in code and are exact.

        First write "summary": feedback on the student's mistakes for the teacher, at most 200 words. Which problems
        keep coming back and which were one-off slips, careless blunders in otherwise sound work as opposed to real
        misunderstandings, where most of the points were lost, and whether the mistakes are shrinking or growing over
        the assignments. Base it only on what you are given and say so when it covers only one or two assignments. Plain
        prose, no lists, no names, and do not invent details of the code.

        Then write "nextSteps": what the student should work on next, at most 100 words. Pick one to three things that
        matter most, recurring and costly problems first, each with a short reason and a concrete way to practise.
        Written for the teacher to pass on, no names.

        Write in English.
        """;

    private const string MistakesSchema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "summary": { "type": "string" },
            "nextSteps": { "type": "string" }
          },
          "required": ["summary", "nextSteps"]
        }
        """;

    private const string StrengthsPrompt =
        """
        You help a university programming teacher (Advanced Java, C++ basics) see what one student is good at across the
        course. The student is called "the student" and is never named. You get every strength the teacher kept in the
        student's approved reports, numbered, in due-date order of their assignments: the assignment, its category,
        whether it is new, consistent (the same category was a strength in an earlier assignment) or improved (the same
        category was a mistake in an earlier assignment), and a short description of what the code showed. The counts at
        the end are exact.

        First group the strengths into skills. Strengths that show the same skill belong together even when they are
        worded differently or come from different assignments, for example "bounds checked in get" and "index validated
        before access". The teacher reads each group as one skill the student has shown, so a good group is one skill
        that fits every strength in it; keep different skills apart even when they share a category. A strength that
        matches no other one is its own group. For each group write:
        - title: at most eight words naming the skill, for example "Validates indices before accessing lists".
        - explanation: one or two sentences on what the student does and how consistently, without counts.
        - strengthIds: the numbers of the strengths in the group.
        Put every strength in exactly one group and use only the numbers you were given.

        Then write "overview": what this student is good at, at most 150 words, for the teacher. Which skills the student
        shows again and again, which appeared once, and where a former weakness became a strength. With few assignments
        there is little to compare; say so instead of drawing conclusions. Plain prose, no lists, no names.

        Write in English.
        """;

    private const string StrengthsSchema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "groups": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title": { "type": "string" },
                  "explanation": { "type": "string" },
                  "strengthIds": { "type": "array", "items": { "type": "integer" } }
                },
                "required": ["title", "explanation", "strengthIds"]
              }
            },
            "overview": { "type": "string" }
          },
          "required": ["groups", "overview"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IStudentMistakeStore _mistakes;
    private readonly IStudentStrengthStore _strengths;
    private readonly ICourseAssignmentStore _assignments;
    private readonly IStudentAnalysisStore _analyses;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly TimeProvider _timeProvider;

    public StudentAnalysisService(
        IStudentMistakeStore mistakes, IStudentStrengthStore strengths, ICourseAssignmentStore assignments,
        IStudentAnalysisStore analyses, ILanguageModel languageModel, ModelCallContext callContext, TimeProvider timeProvider)
    {
        _mistakes = mistakes;
        _strengths = strengths;
        _assignments = assignments;
        _analyses = analyses;
        _languageModel = languageModel;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> AnalyzeAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default)
    {
        var mistakes = await _mistakes.GetByStudentAsync(courseId, studentId, cancellationToken);
        var strengths = await _strengths.GetByStudentAsync(courseId, studentId, cancellationToken);
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);

        DateTimeOffset? DueAt(string id)
        {
            return assignments.GetValueOrDefault(id)?.DueAt;
        }

        string Title(string id)
        {
            return assignments.GetValueOrDefault(id)?.Title ?? "Assignment";
        }

        double? MaxPoints(string id)
        {
            return assignments.GetValueOrDefault(id)?.MaxPoints;
        }

        _callContext.DescribeCourse(courseId);
        _callContext.MarkStudentAnalysis();
        var now = _timeProvider.GetUtcNow();
        StudentAnalysis analysis;

        if (kind == StudentAnalysisKind.Mistakes)
        {
            if (mistakes.Count == 0)
            {
                throw new InvalidRequestException(
                    "This student has no recorded mistakes yet. Approve a report first (Edit draft → Approve).");
            }

            var reply = await _languageModel.AnalyzeAsync(MistakesPrompt,
                [new TextPart(MistakesPromptText(mistakes, DueAt, Title, MaxPoints))], MistakesSchema, cancellationToken);
            var answer = Read<MistakesReply>(reply.Text);
            analysis = new StudentAnalysis(courseId, studentId, kind, reply.Model, answer.Summary ?? "", answer.NextSteps ?? "",
                mistakes.Select(m => m.AssignmentId).Distinct().Count(), mistakes.Count, Latest(mistakes.Select(m => m.ApprovedAt)), now);
        }
        else
        {
            if (strengths.Count == 0)
            {
                throw new InvalidRequestException(
                    "This student has no recorded strengths yet. Strengths are recorded when you approve a report whose AI draft noted some.");
            }

            var ordered = Ordered(strengths, DueAt);
            var reply = await _languageModel.AnalyzeAsync(StrengthsPrompt,
                [new TextPart(StrengthsPromptText(ordered, mistakes, DueAt, Title))], StrengthsSchema, cancellationToken);
            var answer = Read<StrengthsReply>(reply.Text);
            analysis = new StudentAnalysis(courseId, studentId, kind, reply.Model, answer.Overview ?? "", "",
                strengths.Select(s => s.AssignmentId).Distinct().Count(), strengths.Count, Latest(strengths.Select(s => s.ApprovedAt)), now,
                Group(ordered, answer.Groups ?? []));
        }

        _analyses.Add(analysis);
        await _analyses.SaveChangesAsync(cancellationToken);
        return analysis.Id;
    }

    public async Task<StudentAnalysisResponse?> GetLatestAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default)
    {
        var analysis = await _analyses.GetLatestAsync(courseId, studentId, kind, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        var mistakes = await _mistakes.GetByStudentAsync(courseId, studentId, cancellationToken);
        var strengths = await _strengths.GetByStudentAsync(courseId, studentId, cancellationToken);
        var outdated = kind == StudentAnalysisKind.Mistakes
            ? analysis.IsOutdated(mistakes.Count, Latest(mistakes.Select(m => m.ApprovedAt)))
            : analysis.IsOutdated(strengths.Count, Latest(strengths.Select(s => s.ApprovedAt)));

        var groups = new List<StrengthGroupResponse>();
        if (analysis.Groups.Count > 0)
        {
            var titles = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
                .ToDictionary(a => a.ExternalAssignmentId, a => a.Title);
            var byId = strengths.ToDictionary(s => s.Id);
            foreach (var group in analysis.Groups.OrderBy(g => g.Position))
            {
                var members = group.StrengthIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                groups.Add(new StrengthGroupResponse(group.Title, group.Explanation, group.Ungrouped, members.Count,
                    members.Select(m => m.AssignmentId).Distinct().Count(),
                    members.Select(m => m.Category).Distinct().ToList(),
                    members.Select(m => new GroupedStrength(m.Id, titles.GetValueOrDefault(m.AssignmentId), m.Category, m.Message)).ToList()));
            }
        }

        return new StudentAnalysisResponse(analysis.Id, analysis.Kind, analysis.Model, analysis.Summary, analysis.NextSteps,
            analysis.Assignments, analysis.Records, analysis.CreatedAt, outdated, groups);
    }

    internal static string MistakesPromptText(
        IReadOnlyList<StudentMistake> mistakes, Func<string, DateTimeOffset?> dueAt, Func<string, string> title,
        Func<string, double?> maxPoints)
    {
        var labels = MistakeHistory.Label(mistakes, dueAt).ToDictionary(l => l.Mistake.Id);
        var prompt = new StringBuilder("<assignments>\n");
        foreach (var assignment in mistakes.GroupBy(m => m.AssignmentId)
                     .OrderBy(g => dueAt(g.Key) ?? DateTimeOffset.MaxValue)
                     .ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            prompt.Append("<assignment title=\"").Append(title(assignment.Key)).Append('"');
            if (dueAt(assignment.Key) is { } due)
            {
                prompt.Append(" due=\"").Append(due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('"');
            }

            if (maxPoints(assignment.Key) is { } max)
            {
                prompt.Append(" points=\"").Append(Number(max)).Append('"');
            }

            prompt.Append(" lost=\"").Append(Number(assignment.Sum(m => m.Points ?? 0))).AppendLine("\">");
            foreach (var mistake in assignment)
            {
                var label = labels[mistake.Id];
                prompt.Append("- ").Append(mistake.Severity).Append(" · ").Append(mistake.Category)
                    .Append(" · -").Append(Number(mistake.Points ?? 0)).Append(" pts · ")
                    .Append(label.Label == MistakeLabel.Repeated ? $"repeated (first in {title(label.FirstAssignmentId!)})" : "new")
                    .Append(" · ").AppendLine(mistake.Message.ReplaceLineEndings(" "));
            }

            prompt.AppendLine("</assignment>");
        }

        prompt.AppendLine("</assignments>").AppendLine("<trends>");
        foreach (var trend in MistakeHistory.Trends(mistakes, dueAt))
        {
            prompt.Append(trend.Category).Append(": ").Append(trend.Count).Append(" in ").Append(trend.Assignments)
                .Append(trend.Assignments == 1 ? " assignment" : " assignments")
                .Append(trend.Repeated ? ", repeated" : "")
                .Append(", ").Append(Number(trend.PointsLost)).AppendLine(" points lost");
        }

        prompt.AppendLine("</trends>").Append("Write the feedback on this student's mistakes.");
        return prompt.ToString();
    }

    internal static string StrengthsPromptText(
        IReadOnlyList<StudentStrength> ordered, IReadOnlyList<StudentMistake> mistakes, Func<string, DateTimeOffset?> dueAt,
        Func<string, string> title)
    {
        var labels = StrengthHistory.Label(ordered, mistakes, dueAt).ToDictionary(l => l.Strength.Id);
        var prompt = new StringBuilder("<strengths>\n");
        for (var i = 0; i < ordered.Count; i++)
        {
            var strength = ordered[i];
            var label = labels[strength.Id];
            var since = label.SinceAssignmentId is { } sinceId ? title(sinceId) : null;
            prompt.Append('[').Append(i + 1).Append("] ").Append(title(strength.AssignmentId)).Append(" · ").Append(strength.Category)
                .Append(" · ").Append(label.Label switch
                {
                    StrengthLabel.Consistent => $"consistent (also in {since})",
                    StrengthLabel.Improved => $"improved (was a mistake in {since})",
                    _ => "new",
                })
                .Append(" · ").AppendLine(strength.Message.ReplaceLineEndings(" "));
        }

        var assignments = ordered.Select(s => s.AssignmentId).Distinct().Count();
        prompt.AppendLine("</strengths>").AppendLine("<counts>")
            .Append(ordered.Count).Append(ordered.Count == 1 ? " strength" : " strengths").Append(" from ").Append(assignments)
            .AppendLine(assignments == 1 ? " assignment" : " assignments");
        foreach (var trend in StrengthHistory.Trends(ordered, mistakes, dueAt))
        {
            prompt.Append(trend.Category).Append(": ").Append(trend.Count).Append(" in ").Append(trend.Assignments)
                .Append(trend.Assignments == 1 ? " assignment" : " assignments")
                .Append(trend.Consistent ? ", consistent" : "")
                .Append(trend.ImprovedSinceAssignmentId is { } improved ? $", improved since mistakes in {title(improved)}" : "")
                .AppendLine();
        }

        prompt.AppendLine("</counts>").Append("Group these strengths into skills and write the overview.");
        return prompt.ToString();
    }

    internal static IReadOnlyList<StrengthGroup> Group(IReadOnlyList<StudentStrength> ordered, IReadOnlyList<ReplyGroup> groups)
    {
        var used = new HashSet<int>();
        var result = new List<StrengthGroup>();
        foreach (var group in groups)
        {
            var ids = (group.StrengthIds ?? [])
                .Where(id => id >= 1 && id <= ordered.Count && !used.Contains(id))
                .Distinct()
                .ToList();
            if (ids.Count == 0 || string.IsNullOrWhiteSpace(group.Title))
            {
                continue;
            }

            used.UnionWith(ids);
            result.Add(new StrengthGroup(group.Title, group.Explanation ?? "", false, ids.Select(id => ordered[id - 1].Id)));
        }

        var leftover = Enumerable.Range(1, ordered.Count).Where(id => !used.Contains(id)).ToList();
        if (leftover.Count > 0)
        {
            result.Add(new StrengthGroup("Not grouped", "Strengths the analysis did not put in a group.", true,
                leftover.Select(id => ordered[id - 1].Id)));
        }

        return result;
    }

    private static List<StudentStrength> Ordered(IReadOnlyList<StudentStrength> strengths, Func<string, DateTimeOffset?> dueAt)
    {
        return strengths
            .OrderBy(s => dueAt(s.AssignmentId) ?? s.ApprovedAt)
            .ThenBy(s => s.AssignmentId, StringComparer.Ordinal)
            .ToList();
    }

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset> approvedAt)
    {
        var list = approvedAt.ToList();
        return list.Count == 0 ? null : list.Max();
    }

    private static string Number(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static T Read<T>(string reply)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(reply, JsonOptions)
                   ?? throw new LanguageModelUnavailableException("The analysis came back empty.");
        }
        catch (JsonException ex)
        {
            throw new LanguageModelUnavailableException("The analysis answer could not be read.", ex);
        }
    }

    private sealed record MistakesReply(string? Summary, string? NextSteps);

    private sealed record StrengthsReply(List<ReplyGroup>? Groups, string? Overview);

    internal sealed record ReplyGroup(string? Title, string? Explanation, List<int>? StrengthIds);
}
