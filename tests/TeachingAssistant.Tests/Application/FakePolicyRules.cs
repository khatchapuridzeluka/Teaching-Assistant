using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakePolicyRules : IPolicyRuleStore
{
    public List<PolicyRule> All { get; } = [];

    public int Saves { get; private set; }

    public PolicyRule Add(int position, string title, string detail)
    {
        var rule = new PolicyRule(Guid.NewGuid(), position, title, PolicyEffect.NoDeduction, null, null, title, detail,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        All.Add(rule);
        return rule;
    }

    public Task<IReadOnlyList<PolicyRule>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PolicyRule>>(All.Where(r => !r.IsDeleted).OrderBy(r => r.Position).ToList());

    public Task<PolicyRule?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(r => r.Id == id));

    public Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Count == 0 ? 0 : All.Max(r => r.Position));

    void IPolicyRuleStore.Add(PolicyRule rule) => All.Add(rule);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
