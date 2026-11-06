namespace TeachingAssistant.Application.Models;

/// <summary>
/// What fetching an assignment's instructions did: the latest stored version, whether it was just created, and what
/// could not be stored (one sentence each).
/// </summary>
public record InstructionsFetchResult(int VersionNumber, bool IsNew, IReadOnlyList<string> Problems);
