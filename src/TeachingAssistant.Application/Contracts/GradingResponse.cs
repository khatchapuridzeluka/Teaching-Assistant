using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record GradingResponse(
    Guid Id,
    Guid VersionId,
    GradingStatus Status,
    string Model,
    string Review,
    double? Score,
    double? MaxScore,
    double? Bonus,
    IReadOnlyList<CodeMistake> Mistakes,
    int InstructionsVersion,
    IReadOnlyList<string> SentFiles,
    IReadOnlyList<string> SkippedFiles,
    DateTimeOffset CreatedAt);

public record CodeMistake(
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    Guid? Id = null);

public record AssignmentGradingResponse(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    string AssignmentId,
    string SubmissionId,
    string StudentId,
    string StudentName,
    GradingStatus Status,
    bool Current,
    double? Score,
    double? MaxScore,
    double? Bonus,
    int Major,
    int Minor,
    int Style,
    string Model,
    DateTimeOffset CreatedAt,
    ReportStatus? ReportStatus);
