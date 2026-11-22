namespace TeachingAssistant.Application.Models;

public record InstructionsFetchResult
{
    public required int VersionNumber { get; init; }
    public required bool IsNew { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}
