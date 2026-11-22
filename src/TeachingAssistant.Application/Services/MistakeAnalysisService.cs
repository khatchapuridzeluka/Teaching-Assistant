using System.Globalization;
using System.Text;
using System.Text.Json;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Prompts;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Services;

public class MistakeAnalysisService : IMistakeAnalysisService
{
    private const string Schema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "summary": { "type": "string" },
            "nextLecture": { "type": "string" },
            "groups": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title": { "type": "string" },
                  "explanation": { "type": "string" },
                  "mistakeIds": { "type": "array", "items": { "type": "integer" } }
                },
                "required": ["title", "explanation", "mistakeIds"]
              }
            }
          },
          "required": ["summary", "nextLecture", "groups"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISubmissionStore _submissions;
    private readonly IGradingStore _gradings;
    private readonly IGradingReportStore _reports;
    private readonly ICourseAssignmentStore _assignments;
    private readonly IMistakeAnalysisStore _analyses;
    private readonly ILanguageModel _languageModel;
    private readonly ModelCallContext _callContext;
    private readonly IGradingService _grading;
    private readonly TimeProvider _timeProvider;

    public MistakeAnalysisService(
        ISubmissionStore submissions, IGradingStore gradings, IGradingReportStore reports, ICourseAssignmentStore assignments,
        IMistakeAnalysisStore analyses, ILanguageModel languageModel, ModelCallContext callContext, IGradingService grading,
        TimeProvider timeProvider)
    {
        _grading = grading;
        _submissions = submissions;
        _gradings = gradings;
        _reports = reports;
        _assignments = assignments;
        _analyses = analyses;
        _languageModel = languageModel;
        _callContext = callContext;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> AnalyzeAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var (mistakes, scores) = await CollectAsync(courseId, assignmentId, cancellationToken);
        if (scores.Count == 0)
        {
            throw new InvalidRequestException("No student of this assignment has an AI draft yet. Grade the students first.");
        }

        var instructions = await _grading.BuildInstructionsAsync(assignmentId, cancellationToken)
                           ?? throw new InvalidRequestException(
                               "The assignment's instructions are not stored or cannot be read. Fetch the assignment first.");

        _callContext.DescribeAssignment(courseId, assignmentId);
        var parts = instructions.Append(new TextPart { Text = Prompt(mistakes, scores) }).ToList();
        var reply = await _languageModel.AnalyzeAsync(PromptLibrary.ClassMistakes, parts, Schema, cancellationToken);
        var answer = Read(reply.Text);

        var analysis = new MistakeAnalysis(courseId, assignmentId, reply.Model, answer.Summary ?? "", answer.NextLecture ?? "",
            scores.Count, mistakes.Count,
            Cluster(mistakes, answer.Groups ?? []), _timeProvider.GetUtcNow());
        _analyses.Add(analysis);
        await _analyses.SaveChangesAsync(cancellationToken);
        return analysis.Id;
    }

    public async Task<CommonMistakesResponse?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
    {
        var analysis = await _analyses.GetLatestAsync(courseId, assignmentId, cancellationToken);
        return analysis is null ? null : ToResponse(analysis);
    }

    public async Task<MistakeClusterResponse?> ApproveAsync(Guid clusterId, string explanation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new InvalidRequestException("The explanation cannot be empty.");
        }

        var analysis = await _analyses.FindByClusterAsync(clusterId, cancellationToken);
        var cluster = analysis?.Clusters.Single(c => c.Id == clusterId);
        if (analysis is null || cluster is null)
        {
            return null;
        }

        cluster.Approve(explanation, _timeProvider.GetUtcNow());
        await _analyses.SaveChangesAsync(cancellationToken);
        return ToResponse(cluster, analysis.Students);
    }

    internal static IReadOnlyList<MistakeCluster> Cluster(IReadOnlyList<ClusterMember> mistakes, IReadOnlyList<ReplyGroup> groups)
    {
        var used = new HashSet<int>();
        var clusters = new List<MistakeCluster>();
        foreach (var group in groups)
        {
            var ids = (group.MistakeIds ?? [])
                .Where(id => id >= 1 && id <= mistakes.Count && !used.Contains(id))
                .Distinct()
                .ToList();
            if (ids.Count == 0 || string.IsNullOrWhiteSpace(group.Title))
            {
                continue;
            }

            used.UnionWith(ids);
            clusters.Add(new MistakeCluster(group.Title, group.Explanation ?? "", false, ids.Select(id => mistakes[id - 1])));
        }

        var leftover = Enumerable.Range(1, mistakes.Count).Where(id => !used.Contains(id)).Select(id => mistakes[id - 1]).ToList();
        if (leftover.Count > 0)
        {
            clusters.Add(new MistakeCluster("Not grouped", "Mistakes the analysis did not put in a group.", true, leftover));
        }

        return clusters;
    }

    internal static string Prompt(IReadOnlyList<ClusterMember> mistakes, IReadOnlyList<StudentScore> scores)
    {
        var prompt = new StringBuilder();
        var tags = scores.Select((s, i) => (s.StudentId, Tag: $"S{i + 1}")).ToDictionary(x => x.StudentId, x => x.Tag);

        prompt.AppendLine("<scores>");
        foreach (var score in scores)
        {
            prompt.Append(tags[score.StudentId]).Append(": ");
            prompt.Append(score.Score is { } value ? Number(value) : "no score");
            if (score.MaxScore is { } max)
            {
                prompt.Append(" / ").Append(Number(max));
            }

            if (score.Bonus is > 0)
            {
                prompt.Append(" (+").Append(Number(score.Bonus.Value)).Append(" bonus)");
            }

            prompt.Append(", ").Append(score.Mistakes).AppendLine(score.Mistakes == 1 ? " mistake" : " mistakes");
        }

        prompt.AppendLine("</scores>");

        var numbered = mistakes.Select((m, i) => (Number: i + 1, Mistake: m)).ToList();
        foreach (var severity in Enum.GetValues<MistakeSeverity>())
        {
            var ofSeverity = numbered.Where(n => n.Mistake.Severity == severity).ToList();
            if (ofSeverity.Count == 0)
            {
                continue;
            }

            prompt.Append("<mistakes severity=\"").Append(severity).AppendLine("\">");
            foreach (var (number, mistake) in ofSeverity)
            {
                prompt.Append('[').Append(number).Append("] ")
                    .Append(tags.GetValueOrDefault(mistake.StudentId, "S?")).Append(" · ")
                    .Append(mistake.Category);
                if (mistake.Points is { } points)
                {
                    prompt.Append(" · -").Append(Number(points)).Append(" pts");
                }

                if (mistake.File is { } file)
                {
                    prompt.Append(" · ").Append(file[(file.LastIndexOf('/') + 1)..]);
                    if (mistake.StartLine is { } line)
                    {
                        prompt.Append(':').Append(line);
                    }
                }

                prompt.Append(" · ").AppendLine(mistake.Message.ReplaceLineEndings(" "));
            }

            prompt.AppendLine("</mistakes>");
        }

        return prompt.ToString();
    }

    private static string Number(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static Reply Read(string reply)
    {
        try
        {
            return JsonSerializer.Deserialize<Reply>(reply, JsonOptions)
                   ?? throw new LanguageModelUnavailableException("The analysis came back empty.");
        }
        catch (JsonException ex)
        {
            throw new LanguageModelUnavailableException("The analysis answer could not be read.", ex);
        }
    }

    private async Task<(IReadOnlyList<ClusterMember> Mistakes, IReadOnlyList<StudentScore> Scores)> CollectAsync(
        string courseId, string assignmentId, CancellationToken cancellationToken)
    {
        var submissions = (await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
            .Where(s => s.CourseId == courseId && s.LatestVersion is not null)
            .ToDictionary(s => s.LatestVersion!.Id);
        var drafts = (await _gradings.GetByVersionsAsync(submissions.Keys.ToList(), cancellationToken))
            .Where(g => g.Status == GradingStatus.Draft)
            .GroupBy(g => g.VersionId)
            .Select(g => g.MaxBy(d => d.CreatedAt)!)
            .ToList();

        var mistakes = new List<ClusterMember>();
        var scores = new List<StudentScore>();
        foreach (var draft in drafts.OrderBy(d => submissions[d.VersionId].StudentName, StringComparer.Ordinal))
        {
            var submission = submissions[draft.VersionId];
            var report = await _reports.FindByGradingAsync(draft.Id, cancellationToken);
            var before = mistakes.Count;
            if (report is not null)
            {
                mistakes.AddRange(report.Mistakes
                    .Where(m => m.Decision != MistakeDecision.Rejected && m.SourceMistakeId is not null)
                    .OrderBy(m => m.Position)
                    .Select(m => Member(m.SourceMistakeId!.Value, draft.VersionId, submission, m.Severity, m.Category, m.Points,
                        m.Message, m.File, m.StartLine, m.EndLine)));
            }
            else
            {
                mistakes.AddRange(draft.Mistakes
                    .OrderBy(m => m.Position)
                    .Select(m => Member(m.Id, draft.VersionId, submission, m.Severity, m.Category, m.Points, m.Message, m.File,
                        m.StartLine, m.EndLine)));
            }

            scores.Add(new StudentScore
            {
                StudentId = submission.StudentId,
                Score = report?.Score ?? draft.Score,
                MaxScore = draft.MaxScore,
                Bonus = report?.Bonus ?? draft.Bonus,
                Mistakes = mistakes.Count - before,
            });
        }

        return (mistakes, scores);
    }

    private static ClusterMember Member(
        Guid mistakeId, Guid versionId, Submission submission, MistakeSeverity severity, MistakeCategory category, double? points,
        string message, string? file, int? startLine, int? endLine)
    {
        return new ClusterMember(mistakeId, versionId, submission.StudentId, submission.StudentName, submission.ExternalSubmissionId,
            severity, category, points, message, file, startLine, endLine);
    }

    private static CommonMistakesResponse ToResponse(MistakeAnalysis analysis)
    {
        var clusters = analysis.Clusters
            .Select(c => ToResponse(c, analysis.Students))
            .OrderBy(c => c.Ungrouped)
            .ThenByDescending(c => c.Students)
            .ThenByDescending(c => c.PointsLost)
            .ThenBy(c => c.Title)
            .ToList();
        var members = analysis.Clusters.SelectMany(c => c.Members).ToList();
        return new CommonMistakesResponse
        {
            AnalysisId = analysis.Id,
            AnalyzedAt = analysis.CreatedAt,
            Model = analysis.Model,
            Summary = analysis.Summary,
            NextLecture = analysis.NextLecture,
            Students = analysis.Students,
            Mistakes = analysis.Mistakes,
            Major = members.Count(m => m.Severity == MistakeSeverity.Major),
            Minor = members.Count(m => m.Severity == MistakeSeverity.Minor),
            Style = members.Count(m => m.Severity == MistakeSeverity.Style),
            Groups = clusters,
        };
    }

    private static MistakeClusterResponse ToResponse(MistakeCluster cluster, int students)
    {
        var affected = cluster.Members.Select(m => m.StudentId).Distinct().Count();
        var withPoints = cluster.Members.Where(m => m.Points is not null).ToList();
        return new MistakeClusterResponse
        {
            Id = cluster.Id,
            Title = cluster.Title,
            Explanation = cluster.Explanation,
            ApprovedExplanation = cluster.ApprovedExplanation,
            ApprovedAt = cluster.ApprovedAt,
            Ungrouped = cluster.Ungrouped,
            Students = affected,
            StudentPercent = students == 0 ? 0 : Math.Round(affected * 100.0 / students, 1),
            Count = cluster.Members.Count,
            PointsLost = withPoints.Sum(m => m.Points!.Value),
            AveragePoints = withPoints.Count == 0 ? null : Math.Round(withPoints.Average(m => m.Points!.Value), 1),
            Major = cluster.Members.Count(m => m.Severity == MistakeSeverity.Major),
            Minor = cluster.Members.Count(m => m.Severity == MistakeSeverity.Minor),
            Style = cluster.Members.Count(m => m.Severity == MistakeSeverity.Style),
            Members = cluster.Members
                .OrderBy(m => m.StudentName)
                .Select(m => new ClusterMemberResponse
                {
                    Id = m.Id,
                    GradingMistakeId = m.GradingMistakeId,
                    VersionId = m.VersionId,
                    StudentId = m.StudentId,
                    StudentName = m.StudentName,
                    SubmissionId = m.SubmissionId,
                    Severity = m.Severity,
                    Category = m.Category,
                    Points = m.Points,
                    Message = m.Message,
                    File = m.File,
                    StartLine = m.StartLine,
                    EndLine = m.EndLine,
                })
                .ToList(),
        };
    }

    internal sealed record Reply
    {
        public string? Summary { get; init; }
        public string? NextLecture { get; init; }
        public List<ReplyGroup>? Groups { get; init; }
    }

    internal sealed record StudentScore
    {
        public required string StudentId { get; init; }
        public double? Score { get; init; }
        public double? MaxScore { get; init; }
        public double? Bonus { get; init; }
        public required int Mistakes { get; init; }
    }

    internal sealed record ReplyGroup
    {
        public string? Title { get; init; }
        public string? Explanation { get; init; }
        public List<int>? MistakeIds { get; init; }
    }
}
