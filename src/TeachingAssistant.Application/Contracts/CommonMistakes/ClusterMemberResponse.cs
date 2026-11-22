using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record ClusterMemberResponse(
    Guid Id,
    Guid GradingMistakeId,
    Guid VersionId,
    string StudentId,
    string StudentName,
    string SubmissionId,
    MistakeSeverity Severity,
    MistakeCategory Category,
    double? Points,
    string Message,
    string? File,
    int? StartLine,
    int? EndLine);
