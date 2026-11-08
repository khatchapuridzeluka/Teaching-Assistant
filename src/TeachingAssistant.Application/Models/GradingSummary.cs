using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>
/// One saved grading as a list row: the grading's numbers, the student and version it graded, and how many mistakes of
/// each severity it has. Read in one query; the review text and the mistakes themselves are not loaded.
/// </summary>
public record GradingSummary(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    string AssignmentId,
    string SubmissionId,
    string StudentId,
    string StudentName,
    GradingStatus Status,
    double? Score,
    double? MaxScore,
    double? Bonus,
    int Major,
    int Minor,
    int Style,
    string Model,
    DateTimeOffset CreatedAt);
