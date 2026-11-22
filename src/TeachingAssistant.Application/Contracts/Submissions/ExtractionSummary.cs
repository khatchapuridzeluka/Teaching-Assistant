namespace TeachingAssistant.Application.Contracts;

public record ExtractionSummary(int Versions, int VersionsWithCode, IReadOnlyList<FetchProblem> Problems);
