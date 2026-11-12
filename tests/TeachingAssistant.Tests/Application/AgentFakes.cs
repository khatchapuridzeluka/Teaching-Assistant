using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeAgentChats : IAgentChatStore
{
    public List<AgentConversation> Conversations { get; } = [];

    public List<AgentRun> Runs { get; } = [];

    public List<AgentMessage> Messages { get; } = [];

    public int Saves { get; private set; }

    public Task<AgentConversation?> FindConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Conversations.FirstOrDefault(c => c.Id == conversationId));

    public Task<AgentRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.Id == runId));

    public Task<AgentRun?> FindUnfinishedRunAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.ConversationId == conversationId && !r.IsFinished));

    public Task<IReadOnlyList<AgentRun>> GetRunsWaitingOnAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>(Runs.Where(r => r.Status == AgentRunStatus.Waiting && r.WaitingJobIds.Contains(jobId)).ToList());

    public Task<IReadOnlyList<AgentRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentRun>>(Runs.Where(r => !r.IsFinished).ToList());

    public Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.ConversationId == conversationId).OrderBy(m => m.Sequence).ToList());

    public Task<IReadOnlyList<AgentMessage>> GetRunMessagesAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentMessage>>(Messages.Where(m => m.RunId == runId).OrderBy(m => m.Sequence).ToList());

    public void Add(AgentConversation conversation) => Conversations.Add(conversation);

    public void Add(AgentRun run) => Runs.Add(run);

    public void Add(AgentMessage message) => Messages.Add(message);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAgentJobs : IJobStore, IJobSignal, IGradingStore
{
    public List<Job> Jobs { get; } = [];

    public List<JobSubject> Subjects { get; } = [];

    public List<AiGrading> Gradings { get; } = [];

    public int Notified { get; private set; }

    public void Notify() => Notified++;

    public Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default)
    {
        Jobs.Add(job);
        return Task.FromResult(job);
    }

    public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.Id == jobId));

    public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<JobSubject>>(Subjects.Where(s => versionIds.Contains(s.VersionId)).ToList());

    Task<AiGrading?> IGradingStore.FindAsync(Guid gradingId, CancellationToken cancellationToken) =>
        Task.FromResult(Gradings.FirstOrDefault(g => g.Id == gradingId));

    public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiGrading>>(Gradings.Where(g => versionIds.Contains(g.VersionId)).ToList());

    public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Add(AiGrading grading) => Gradings.Add(grading);

    public Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetRecentAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
        DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(
        IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
