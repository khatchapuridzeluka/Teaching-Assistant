using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Infrastructure.Persistence;

namespace TeachingAssistant.Tests;

public class RecordsTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(AgentRun).Assembly,
        typeof(AgentRunResponse).Assembly,
        typeof(AppDbContext).Assembly,
        typeof(RecordsTests).Assembly,
    ];

    [Fact]
    public void Records_AreDeclaredWithInitProperties_NotPositionally()
    {
        // A positional record has no parameterless constructor; one declared with init properties always does.
        var records = Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.GetMethod("<Clone>$") is not null)
            .ToArray();

        var positional = records
            .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes) is null)
            .Select(t => t.FullName)
            .ToArray();

        Assert.NotEmpty(records);
        Assert.Empty(positional);
    }
}
