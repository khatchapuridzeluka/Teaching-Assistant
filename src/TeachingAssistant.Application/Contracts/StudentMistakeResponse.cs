using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentMistakeFilter(
    string? AssignmentId = null,
    MistakeCategory? Category = null,
    MistakeSeverity? Severity = null,
    MistakeLabel? Label = null);

public record StudentMistakesResponse(
    int Total,
    int Recorded,
    double PointsLost,
    double? ClassroomPointsLost,
    int ClassroomGraded,
    IReadOnlyList<StudentMistakeResponse> Mistakes,
    IReadOnlyList<MistakeCategoryResponse> Categories,
    IReadOnlyList<MistakeShare<MistakeSeverity>> Severities,
    IReadOnlyList<MistakeShare<MistakeLabel>> Labels,
    IReadOnlyList<MistakeAssignmentResponse> Assignments,
    IReadOnlyList<MistakeAssignmentOption> AssignmentOptions);

public record StudentMistakeResponse(
    Guid Id,
    string AssignmentId,
    string? AssignmentTitle,
    DateTimeOffset? AssignmentDueAt,
    Guid VersionId,
    Guid? ReportId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message,
    double? Points,
    MistakeDecision Decision,
    MistakeLabel Label,
    string? RepeatedFromAssignmentTitle,
    DateTimeOffset ApprovedAt,
    StudentRecordSource Source);

public record MistakeCategoryResponse(
    MistakeCategory Category,
    int Count,
    double Percent,
    int Assignments,
    bool Repeated,
    double PointsLost,
    string? FirstAssignmentTitle,
    string? LastAssignmentTitle);

public record MistakeShare<T>(T Key, int Count, double Percent);

public record MistakeAssignmentResponse(
    string AssignmentId,
    string? Title,
    DateTimeOffset? DueAt,
    int Total,
    int Major,
    int Minor,
    int Style,
    int Repeated,
    double PointsLost,
    double? MaxPoints,
    double? ClassroomGrade,
    double? ReportScore,
    double? ReportBonus,
    double? AiScore,
    double? AiBonus,
    StudentRecordSource Source);

public record MistakeAssignmentOption(string AssignmentId, string? Title, DateTimeOffset? DueAt, int Count);
