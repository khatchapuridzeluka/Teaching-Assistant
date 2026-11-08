namespace TeachingAssistant.Application.Models;

public record InstructionsFetchResult(int VersionNumber, bool IsNew, IReadOnlyList<string> Problems);
