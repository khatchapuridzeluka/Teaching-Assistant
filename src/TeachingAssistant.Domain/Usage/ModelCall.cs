namespace TeachingAssistant.Domain.Usage;

public enum ModelCallPurpose
{
    Grading,

    GradeAll,

    Chat,

    HealthCheck,

    MistakeAnalysis,

    StudentAnalysis,
}

public class ModelCall
{
    private ModelCall()
    {
    }

    private ModelCall(ModelCallPurpose purpose, string requestedModel, DateTimeOffset startedAt, TimeSpan duration, ModelCallSubject? subject)
    {
        Id = Guid.CreateVersion7();
        Purpose = purpose;
        RequestedModel = requestedModel;
        CreatedAt = startedAt.ToUniversalTime();
        DurationMs = (long)Math.Max(0, duration.TotalMilliseconds);
        CourseId = subject?.CourseId;
        AssignmentId = subject?.AssignmentId;
        VersionId = subject?.VersionId;
    }

    public Guid Id { get; private set; }

    public ModelCallPurpose Purpose { get; private set; }

    public string RequestedModel { get; private set; } = null!;

    public string? Model { get; private set; }

    public bool Succeeded { get; private set; }

    public string? Error { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public long? CacheReadTokens { get; private set; }

    public long? CacheWriteTokens { get; private set; }

    public long DurationMs { get; private set; }

    public string? CourseId { get; private set; }

    public string? AssignmentId { get; private set; }

    public Guid? VersionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ModelCall Success(
        ModelCallPurpose purpose, string requestedModel, string model, ModelCallTokens? tokens, DateTimeOffset startedAt,
        TimeSpan duration, ModelCallSubject? subject)
    {
        return new ModelCall(purpose, requestedModel, startedAt, duration, subject)
        {
            Model = model,
            Succeeded = true,
            InputTokens = tokens?.Input,
            OutputTokens = tokens?.Output,
            CacheReadTokens = tokens?.CacheRead,
            CacheWriteTokens = tokens?.CacheWrite,
        };
    }

    public static ModelCall Failure(
        ModelCallPurpose purpose, string requestedModel, string error, DateTimeOffset startedAt, TimeSpan duration,
        ModelCallSubject? subject)
    {
        return new ModelCall(purpose, requestedModel, startedAt, duration, subject)
        {
            Succeeded = false,
            Error = error.Length > 1000 ? error[..1000] : error,
        };
    }
}

public sealed record ModelCallTokens(long Input, long Output, long CacheRead, long CacheWrite);

public sealed record ModelCallSubject(string? CourseId, string? AssignmentId, Guid? VersionId);
