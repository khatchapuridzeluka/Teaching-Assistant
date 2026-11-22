namespace TeachingAssistant.Application.Contracts;

public record StoredSubmissionResponse(
    Guid Id,
    string ExternalSubmissionId,
    string StudentName,
    IReadOnlyList<StoredVersionResponse> Versions);
