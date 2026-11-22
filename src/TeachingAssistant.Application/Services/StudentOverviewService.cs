using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentOverviewService : IStudentOverviewService
{
    private const int TopCategories = 3;

    private readonly StudentRecordCollector _records;
    private readonly ICourseAssignmentStore _assignments;

    public StudentOverviewService(StudentRecordCollector records, ICourseAssignmentStore assignments)
    {
        _records = records;
        _assignments = assignments;
    }

    public async Task<StudentOverviewResponse> GetAsync(string courseId, string studentId, CancellationToken cancellationToken = default)
    {
        var records = await _records.CollectAsync(courseId, studentId, cancellationToken);
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);
        return Build(records, assignments);
    }

    /// <summary>
    /// Counts the whole picture of one student from their current records: a score per graded assignment (your report's
    /// score where you reviewed it, otherwise the AI draft's), mistakes and strengths per assignment and per category, and
    /// the categories that cost the most and that the student shows most. The AI overview is written from the same numbers.
    /// </summary>
    internal static StudentOverviewResponse Build(StudentRecords records, IReadOnlyDictionary<string, CourseAssignment> assignments)
    {
        DateTimeOffset? DueAt(string assignmentId)
        {
            return assignments.GetValueOrDefault(assignmentId)?.DueAt;
        }

        string? Title(string? assignmentId)
        {
            return assignmentId is null ? null : assignments.GetValueOrDefault(assignmentId)?.Title;
        }

        var mistakes = MistakeHistory.Label(records.Mistakes, DueAt);
        var strengths = StrengthHistory.Label(records.Strengths, records.Mistakes, DueAt);

        var rows = records.Assignments.Values
            .OrderBy(a => DueAt(a.AssignmentId) ?? DateTimeOffset.MaxValue)
            .ThenBy(a => a.AssignmentId, StringComparer.Ordinal)
            .Select(a =>
            {
                var own = mistakes.Where(l => l.Mistake.AssignmentId == a.AssignmentId).ToList();
                var shown = strengths.Where(l => l.Strength.AssignmentId == a.AssignmentId).ToList();
                var reviewed = a.Source != StudentRecordSource.Draft;
                var score = reviewed ? a.Score?.Score : a.Score?.AiScore;
                var bonus = reviewed ? a.Score?.Bonus : a.Score?.AiBonus;
                var maxPoints = assignments.GetValueOrDefault(a.AssignmentId)?.MaxPoints ?? a.Score?.MaxScore;
                double? percent = score is { } s && maxPoints is > 0 ? Math.Round(s * 100 / maxPoints.Value, 1) : null;
                return new OverviewAssignmentResponse
                {
                    AssignmentId = a.AssignmentId,
                    Title = Title(a.AssignmentId),
                    DueAt = DueAt(a.AssignmentId),
                    Source = a.Source,
                    Score = score,
                    Bonus = bonus,
                    MaxPoints = maxPoints,
                    Percent = percent,
                    AiScore = a.Score?.AiScore,
                    Mistakes = own.Count,
                    Major = own.Count(l => l.Mistake.Severity == MistakeSeverity.Major),
                    Repeated = own.Count(l => l.Label == MistakeLabel.Repeated),
                    PointsLost = own.Sum(l => l.Mistake.Points ?? 0),
                    Strengths = shown.Count,
                    Improved = shown.Count(l => l.Label == StrengthLabel.Improved),
                };
            })
            .ToList();

        var percents = rows.Where(r => r.Percent is not null).Select(r => r.Percent!.Value).ToList();

        var topMistakes = MistakeHistory.Trends(records.Mistakes, DueAt)
            .Take(TopCategories)
            .Select(t => new MistakeCategoryResponse
            {
                Category = t.Category,
                Count = t.Count,
                Percent = Percent(t.Count, records.Mistakes.Count),
                Assignments = t.Assignments,
                Repeated = t.Repeated,
                PointsLost = t.PointsLost,
                FirstAssignmentTitle = Title(t.FirstAssignmentId),
                LastAssignmentTitle = Title(t.LastAssignmentId),
            })
            .ToList();

        var topStrengths = StrengthHistory.Trends(records.Strengths, records.Mistakes, DueAt)
            .Take(TopCategories)
            .Select(t => new StrengthCategoryResponse
            {
                Category = t.Category,
                Count = t.Count,
                Percent = Percent(t.Count, records.Strengths.Count),
                Assignments = t.Assignments,
                Consistent = t.Consistent,
                Mistakes = t.Mistakes,
                ImprovedSinceAssignmentTitle = Title(t.ImprovedSinceAssignmentId),
                FirstAssignmentTitle = Title(t.FirstAssignmentId),
                LastAssignmentTitle = Title(t.LastAssignmentId),
            })
            .ToList();

        var categories = records.Mistakes.Select(m => m.Category)
            .Concat(records.Strengths.Select(s => s.Category))
            .Distinct()
            .Select(c => new OverviewCategoryResponse
            {
                Category = c,
                Strengths = records.Strengths.Count(s => s.Category == c),
                Mistakes = records.Mistakes.Count(m => m.Category == c),
                PointsLost = records.Mistakes.Where(m => m.Category == c).Sum(m => m.Points ?? 0),
            })
            .OrderByDescending(c => c.Strengths + c.Mistakes)
            .ThenBy(c => c.Category)
            .ToList();

        return new StudentOverviewResponse
        {
            GradedAssignments = rows.Count,
            AveragePercent = percents.Count == 0 ? null : Math.Round(percents.Average(), 1),
            FirstPercent = percents.Count == 0 ? null : percents[0],
            LatestPercent = percents.Count == 0 ? null : percents[^1],
            Mistakes = mistakes.Count,
            RepeatedMistakes = mistakes.Count(l => l.Label == MistakeLabel.Repeated),
            PointsLost = records.Mistakes.Sum(m => m.Points ?? 0),
            Strengths = strengths.Count,
            ConsistentStrengths = strengths.Count(l => l.Label == StrengthLabel.Consistent),
            ImprovedStrengths = strengths.Count(l => l.Label == StrengthLabel.Improved),
            Assignments = rows,
            TopMistakes = topMistakes,
            TopStrengths = topStrengths,
            Categories = categories,
        };
    }

    private static double Percent(int count, int total)
    {
        return total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);
    }
}
