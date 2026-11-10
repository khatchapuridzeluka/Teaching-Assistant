namespace TeachingAssistant.Application.Models;

public record GradeAllPlan(
    IReadOnlyList<Guid> VersionIds,
    int Students,
    int AlreadyDrafted,
    int NoWork,
    IReadOnlyList<string> Problems,
    IReadOnlyDictionary<Guid, string>? AssignmentIds = null,
    int NotFetched = 0);

public record JobChildCount(Guid ParentId, Domain.Jobs.JobStatus Status, int Count);
