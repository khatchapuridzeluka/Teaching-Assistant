namespace TeachingAssistant.Domain.Ingestion;

public readonly record struct ContentFingerprint(ContentOrigin Origin, string ExternalId, string Fingerprint);
