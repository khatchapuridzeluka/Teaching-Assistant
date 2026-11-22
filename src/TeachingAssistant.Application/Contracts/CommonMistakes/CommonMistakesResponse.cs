namespace TeachingAssistant.Application.Contracts;

public record CommonMistakesResponse(
    Guid AnalysisId,
    DateTimeOffset AnalyzedAt,
    string Model,
    string Summary,
    string NextLecture,
    int Students,
    int Mistakes,
    int Major,
    int Minor,
    int Style,
    IReadOnlyList<MistakeClusterResponse> Groups);
