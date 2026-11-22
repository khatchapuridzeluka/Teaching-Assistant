using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Application.Models;

public record ExtractableFile
{
    public required string StoredName { get; init; }
    public required ContentKind Kind { get; init; }
}
