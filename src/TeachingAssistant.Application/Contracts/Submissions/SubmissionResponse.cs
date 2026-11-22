namespace TeachingAssistant.Application.Contracts;

public record SubmissionResponse
{
    public required string Id { get; init; }
    public required string AssignmentId { get; init; }
    public required string StudentId { get; init; }
    public required string StudentName { get; init; }
    public required string State { get; init; }
    public required bool Late { get; init; }
    public DateTimeOffset? TurnedInAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public required IReadOnlyList<SubmissionAttachment> Attachments { get; init; }
    public string? Link { get; init; }
    public double? AssignedGrade { get; init; }
    public required IReadOnlyList<GradeChange> GradeHistory { get; init; }
}
