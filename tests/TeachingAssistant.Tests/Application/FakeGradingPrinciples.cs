using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeGradingPrinciples : IGradingPrincipleStore
{
    public List<GradingPrinciple> All { get; } = [];

    public int Saves { get; private set; }

    public GradingPrinciple Add(int position, string title, string detail)
    {
        var principle = new GradingPrinciple(Guid.NewGuid(), position, title, detail, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        All.Add(principle);
        return principle;
    }

    public Task<IReadOnlyList<GradingPrinciple>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GradingPrinciple>>(All.Where(p => !p.IsDeleted).OrderBy(p => p.Position).ToList());

    public Task<GradingPrinciple?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(p => p.Id == id));

    public Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(All.Count == 0 ? 0 : All.Max(p => p.Position));

    void IGradingPrincipleStore.Add(GradingPrinciple principle) => All.Add(principle);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
