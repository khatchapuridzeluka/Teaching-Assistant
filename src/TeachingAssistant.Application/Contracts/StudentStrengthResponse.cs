using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Contracts;

public record StudentStrengthFilter(
    string? AssignmentId = null,
    MistakeCategory? Category = null,
    StrengthLabel? Label = null);

public record StudentStrengthsResponse(
    int Total,
    int Recorded,
    int RecordedMistakes,
    IReadOnlyList<StudentStrengthResponse> Strengths,
    IReadOnlyList<StrengthCategoryResponse> Categories,
    IReadOnlyList<MistakeShare<StrengthLabel>> Labels,
    IReadOnlyList<StrengthAssignmentResponse> Assignments,
    IReadOnlyList<MistakeAssignmentOption> AssignmentOptions);

public record StudentStrengthResponse(
    Guid Id,
    string AssignmentId,
    string? AssignmentTitle,
    DateTimeOffset? AssignmentDueAt,
    Guid VersionId,
    Guid? ReportId,
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeCategory Category,
    string Message,
    MistakeDecision Decision,
    StrengthLabel Label,
    string? SinceAssignmentTitle,
    DateTimeOffset ApprovedAt,
    StudentRecordSource Source);

public record StrengthCategoryResponse(
    MistakeCategory Category,
    int Count,
    double Percent,
    int Assignments,
    bool Consistent,
    int Mistakes,
    string? ImprovedSinceAssignmentTitle,
    string? FirstAssignmentTitle,
    string? LastAssignmentTitle);

public record StrengthAssignmentResponse(
    string AssignmentId,
    string? Title,
    DateTimeOffset? DueAt,
    int Strengths,
    int Mistakes);
