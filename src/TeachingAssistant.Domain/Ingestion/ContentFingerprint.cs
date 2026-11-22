namespace TeachingAssistant.Domain.Ingestion;

public readonly record struct ContentFingerprint
{
    public required ContentOrigin Origin { get; init; }
    public required string ExternalId { get; init; }
    public required string Fingerprint { get; init; }
}
