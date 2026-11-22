using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One discarded draft: whose and which assignment, the model, the AI's total (score plus bonus) and mistakes per severity,
/// why it was discarded and the teacher's note, and the status its report had, if one was made.
/// </summary>
public record DiscardedRow(
    Guid GradingId,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string StudentId,
    string StudentName,
    string Model,
    double? AiScore,
    double? MaxScore,
    int Major,
    int Minor,
    int Style,
    DiscardReason? Reason,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DiscardedAt,
    ReportStatus? ReportStatus);
