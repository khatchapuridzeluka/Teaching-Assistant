namespace TeachingAssistant.Application.Contracts;

public record GradeAllResponse(
    JobResponse? Job,
    int Queued,
    int Students,
    int AlreadyDrafted,
    int AlreadyQueued,
    int NoWork,
    int NotFetched,
    IReadOnlyList<string> Problems);
