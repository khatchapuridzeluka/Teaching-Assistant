using System.Globalization;
using System.Text;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentAnalysisService : IStudentAnalysisService
{
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

    private readonly StudentRecordCollector _records;
    private readonly ICourseAssignmentStore _assignments;
    private readonly IStudentAnalysisStore _analyses;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly TimeProvider _timeProvider;

    public StudentAnalysisService(
        StudentRecordCollector records, ICourseAssignmentStore assignments, IStudentAnalysisStore analyses, ILanguageModel languageModel,
        ModelCallContext callContext, TimeProvider timeProvider)
    {
        _records = records;
        _assignments = assignments;
        _analyses = analyses;
        _languageModel = languageModel;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> AnalyzeAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default)
    {
        var records = await _records.CollectAsync(courseId, studentId, cancellationToken);
        var (mistakes, strengths) = (records.Mistakes, records.Strengths);
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
                    "This student has no mistakes in any graded assignment yet: no current AI draft or report lists one. Grade an assignment first.");
            }

            var reply = await _languageModel.AnalyzeAsync(PromptLibrary.StudentMistakes,
                [new TextPart(MistakesPromptText(mistakes, DueAt, Title, MaxPoints))], MistakesSchema, cancellationToken);
            var answer = Read<MistakesReply>(reply.Text);
            analysis = new StudentAnalysis(courseId, studentId, kind, reply.Model, answer.Summary ?? "", answer.NextSteps ?? "",
                mistakes.Select(m => m.AssignmentId).Distinct().Count(), mistakes.Count, Latest(mistakes.Select(m => m.ApprovedAt)), now);
        }
        else if (kind == StudentAnalysisKind.Strengths)
        {
            if (strengths.Count == 0)
            {
                throw new InvalidRequestException(
                    "This student has no strengths in any graded assignment yet: no current AI draft or report lists one. Grade an assignment first.");
            }

            var ordered = Ordered(strengths, DueAt);
            var reply = await _languageModel.AnalyzeAsync(PromptLibrary.StudentStrengths,
                [new TextPart(StrengthsPromptText(ordered, mistakes, DueAt, Title))], StrengthsSchema, cancellationToken);
            var answer = Read<StrengthsReply>(reply.Text);
            analysis = new StudentAnalysis(courseId, studentId, kind, reply.Model, answer.Overview ?? "", "",
                strengths.Select(s => s.AssignmentId).Distinct().Count(), strengths.Count, Latest(strengths.Select(s => s.ApprovedAt)), now,
                Group(ordered, answer.Groups ?? []));
        }
        else
        {
            if (records.Assignments.Count == 0)
            {
                throw new InvalidRequestException(
                    "This student has no graded assignment yet: there is no current AI draft or report. Grade an assignment first.");
            }

            var reply = await _languageModel.AnalyzeAsync(PromptLibrary.StudentOverview,
                [new TextPart(OverviewPromptText(records, assignments))], MistakesSchema, cancellationToken);
            var answer = Read<MistakesReply>(reply.Text);
            analysis = new StudentAnalysis(courseId, studentId, kind, reply.Model, answer.Summary ?? "", answer.NextSteps ?? "",
                records.Assignments.Count, mistakes.Count + strengths.Count, LatestOf(mistakes, strengths), now);
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

        var (mistakes, strengths, _) = await _records.CollectAsync(courseId, studentId, cancellationToken);
        var outdated = kind switch
        {
            StudentAnalysisKind.Mistakes => analysis.IsOutdated(mistakes.Count, Latest(mistakes.Select(m => m.ApprovedAt))),
            StudentAnalysisKind.Strengths => analysis.IsOutdated(strengths.Count, Latest(strengths.Select(s => s.ApprovedAt))),
            _ => analysis.IsOutdated(mistakes.Count + strengths.Count, LatestOf(mistakes, strengths)),
        };

        var groups = new List<StrengthGroupResponse>();
        if (analysis.Groups.Count > 0)
        {
            var titles = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
                .ToDictionary(a => a.ExternalAssignmentId, a => a.Title);
            var byId = strengths.ToDictionary(Key);
            foreach (var group in analysis.Groups.OrderBy(g => g.Position))
            {
                var members = group.StrengthIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                groups.Add(new StrengthGroupResponse(group.Title, group.Explanation, group.Ungrouped, members.Count,
                    members.Select(m => m.AssignmentId).Distinct().Count(),
                    members.Select(m => m.Category).Distinct().ToList(),
                    members.Select(m => new GroupedStrength(Key(m), titles.GetValueOrDefault(m.AssignmentId), m.Category, m.Message)).ToList()));
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

    internal static string OverviewPromptText(StudentRecords records, IReadOnlyDictionary<string, CourseAssignment> assignments)
    {
        DateTimeOffset? DueAt(string id)
        {
            return assignments.GetValueOrDefault(id)?.DueAt;
        }

        string Title(string? id)
        {
            return (id is null ? null : assignments.GetValueOrDefault(id)?.Title) ?? "Assignment";
        }

        var overview = StudentOverviewService.Build(records, assignments);
        var mistakeLabels = MistakeHistory.Label(records.Mistakes, DueAt).ToDictionary(l => l.Mistake.Id);
        var strengthLabels = StrengthHistory.Label(records.Strengths, records.Mistakes, DueAt).ToDictionary(l => l.Strength.Id);

        var prompt = new StringBuilder("<assignments>\n");
        foreach (var row in overview.Assignments)
        {
            prompt.Append("<assignment title=\"").Append(Title(row.AssignmentId)).Append('"');
            if (row.DueAt is { } due)
            {
                prompt.Append(" due=\"").Append(due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('"');
            }

            prompt.Append(" score=\"").Append(row.Score is { } score ? Number(score) : "unknown");
            if (row.MaxPoints is { } max)
            {
                prompt.Append(" / ").Append(Number(max));
            }

            prompt.Append('"');
            if (row.Bonus is > 0)
            {
                prompt.Append(" bonus=\"").Append(Number(row.Bonus.Value)).Append('"');
            }

            prompt.Append(" checked=\"").Append(row.Source == StudentRecordSource.Draft ? "AI draft, not reviewed" : "teacher's report")
                .AppendLine("\">");
            foreach (var mistake in records.Mistakes.Where(m => m.AssignmentId == row.AssignmentId))
            {
                var label = mistakeLabels[mistake.Id];
                prompt.Append("- mistake · ").Append(mistake.Severity).Append(" · ").Append(mistake.Category)
                    .Append(" · -").Append(Number(mistake.Points ?? 0)).Append(" pts · ")
                    .Append(label.Label == MistakeLabel.Repeated ? $"repeated (first in {Title(label.FirstAssignmentId)})" : "new")
                    .Append(" · ").AppendLine(mistake.Message.ReplaceLineEndings(" "));
            }

            foreach (var strength in records.Strengths.Where(s => s.AssignmentId == row.AssignmentId))
            {
                var label = strengthLabels[strength.Id];
                prompt.Append("- strength · ").Append(strength.Category).Append(" · ").Append(label.Label switch
                    {
                        StrengthLabel.Consistent => $"consistent (also in {Title(label.SinceAssignmentId)})",
                        StrengthLabel.Improved => $"improved (was a mistake in {Title(label.SinceAssignmentId)})",
                        _ => "new",
                    })
                    .Append(" · ").AppendLine(strength.Message.ReplaceLineEndings(" "));
            }

            prompt.AppendLine("</assignment>");
        }

        prompt.AppendLine("</assignments>").AppendLine("<numbers>")
            .Append(overview.GradedAssignments).Append(overview.GradedAssignments == 1 ? " graded assignment" : " graded assignments");
        if (overview.AveragePercent is { } average)
        {
            prompt.Append(", average score ").Append(Number(average)).Append('%')
                .Append(", first ").Append(Number(overview.FirstPercent!.Value)).Append("%, latest ")
                .Append(Number(overview.LatestPercent!.Value)).Append('%');
        }

        prompt.AppendLine()
            .Append(overview.Mistakes).Append(" mistakes (").Append(overview.RepeatedMistakes).Append(" repeated), ")
            .Append(Number(overview.PointsLost)).AppendLine(" points lost")
            .Append(overview.Strengths).Append(" strengths (").Append(overview.ConsistentStrengths).Append(" consistent, ")
            .Append(overview.ImprovedStrengths).AppendLine(" improved from an earlier mistake)")
            .AppendLine("Per category (strengths / mistakes / points lost):");
        foreach (var category in overview.Categories)
        {
            prompt.Append(category.Category).Append(": ").Append(category.Strengths).Append(" / ").Append(category.Mistakes)
                .Append(" / ").AppendLine(Number(category.PointsLost));
        }

        prompt.AppendLine("</numbers>").Append("Write the overall picture of this student.");
        return prompt.ToString();
    }

    private static DateTimeOffset? LatestOf(IReadOnlyList<StudentMistake> mistakes, IReadOnlyList<StudentStrength> strengths)
    {
        return Latest(mistakes.Select(m => m.ApprovedAt).Concat(strengths.Select(s => s.ApprovedAt)));
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
            result.Add(new StrengthGroup(group.Title, group.Explanation ?? "", false, ids.Select(id => Key(ordered[id - 1]))));
        }

        var leftover = Enumerable.Range(1, ordered.Count).Where(id => !used.Contains(id)).ToList();
        if (leftover.Count > 0)
        {
            result.Add(new StrengthGroup("Not grouped", "Strengths the analysis did not put in a group.", true,
                leftover.Select(id => Key(ordered[id - 1]))));
        }

        return result;
    }

    internal static Guid Key(StudentStrength strength)
    {
        return strength.SourceStrengthId ?? strength.ReportStrengthId;
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
