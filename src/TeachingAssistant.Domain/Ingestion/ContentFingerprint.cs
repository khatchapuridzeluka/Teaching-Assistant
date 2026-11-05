namespace TeachingAssistant.Domain.Ingestion;

/// <summary>
/// Identifies one piece of content as the source reports it right now, without downloading it:
/// a Drive file's ID and md5, or a repository URL and commit SHA.
/// </summary>
public readonly record struct ContentFingerprint(ContentOrigin Origin, string ExternalId, string Fingerprint);
