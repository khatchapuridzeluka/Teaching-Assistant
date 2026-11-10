using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingReportResponse(
    Guid Id,
    Guid GradingId,
    Guid VersionId,
    string StudentId,
    string CourseId,
    string AssignmentId,
    ReportStatus Status,
    double? Score,
    double? MaxScore,
    double? Bonus,
    string Review,
    IReadOnlyList<ReportMistakeResponse> Mistakes,
    IReadOnlyList<ReportStrengthResponse> Strengths,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ApprovedAt);

public record ReportMistakeResponse(
    Guid Id,
    Guid? SourceMistakeId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points,
    MistakeDecision Decision);

public record ReportStrengthResponse(
    Guid Id,
    Guid? SourceStrengthId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeCategory Category,
    string Message,
    MistakeDecision Decision);

public record SaveGradingReportRequest(
    double? Score,
    double? Bonus,
    string Review,
    IReadOnlyList<ReportMistakeInput> Mistakes,
    IReadOnlyList<ReportStrengthInput>? Strengths = null);

public record ReportStrengthInput(
    Guid SourceStrengthId,
    bool Rejected,
    MistakeCategory Category,
    string Message);

public record ReportMistakeInput(
    Guid SourceMistakeId,
    bool Rejected,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points = null);
