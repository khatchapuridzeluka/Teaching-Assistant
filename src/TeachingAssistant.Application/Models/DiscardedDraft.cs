using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Models;

/// <summary>A discarded draft with where it belongs and why it was discarded.</summary>
public record DiscardedDraft(
    Guid GradingId,
    string CourseId,
    string AssignmentId,
    string StudentId,
    string StudentName,
    string Model,
    double? Score,
    double? Bonus,
    double? MaxScore,
    int Major,
    int Minor,
    int Style,
    DiscardReason? Reason,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DiscardedAt,
    ReportStatus? ReportStatus);
