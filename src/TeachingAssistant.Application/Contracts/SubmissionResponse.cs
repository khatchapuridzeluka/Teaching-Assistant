namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One student's submission to an assignment, with its attachments as metadata (nothing is downloaded).
/// State is the source's own state, e.g. NEW, CREATED, TURNED_IN, RETURNED or RECLAIMED_BY_STUDENT for Classroom.
/// TurnedInAt is the latest turn-in, so it moves forward when the student resubmits.
/// </summary>
public record SubmissionResponse(
    string Id,
    string StudentId,
    string StudentName,
    string State,
    bool Late,
    DateTimeOffset? TurnedInAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<SubmissionAttachment> Attachments,
    string? Link);

/// <summary>
/// A file or link attached to a submission. FileId is set for Drive files and used to download them later;
/// Url is the link itself (e.g. a GitHub repository) or the Drive file's web address.
/// </summary>
public record SubmissionAttachment(
    AttachmentKind Kind,
    string Title,
    string? Url,
    string? FileId);

/// <summary>
/// What a submission attachment is.
/// </summary>
public enum AttachmentKind
{
    /// <summary>A file in Google Drive (zip, .java file, document).</summary>
    DriveFile,

    /// <summary>A URL, such as a GitHub repository.</summary>
    Link,

    /// <summary>Anything else (YouTube video, Google Form); not gradable as code.</summary>
    Other,
}
