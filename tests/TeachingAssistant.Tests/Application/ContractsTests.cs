using System.Reflection;
using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Tests.Application;

public class ContractsTests
{
    private const string ContractsNamespace = "TeachingAssistant.Application.Contracts";

    private static readonly Type[] Contracts = typeof(AgentRunResponse).Assembly.GetTypes()
        .Where(t => t.Namespace == ContractsNamespace && !t.Name.StartsWith('<'))
        .ToArray();

    [Fact]
    public void Contracts_AreFound()
    {
        Assert.NotEmpty(Contracts);
    }

    [Fact]
    public void Contracts_HaveNoNestedTypes()
    {
        // Swashbuckle names a nested type by its inner name alone, so a nested "Kind" would become a schema called "Kind".
        var nested = Contracts.Where(t => t.IsNested).Select(t => t.FullName).ToArray();

        Assert.Empty(nested);
    }

    [Fact]
    public void SchemaNames_AreUnique_AcrossEveryTypeTheContractsExpose()
    {
        // Swagger has no namespaces: every type the API returns or accepts, Domain enums included, becomes a schema named
        // after its bare type name, and the frontend's type aliases are generated from those names.
        var exposed = new HashSet<Type>();
        foreach (var contract in Contracts)
        {
            Collect(contract, exposed);
        }

        var clashes = exposed
            .GroupBy(SchemaName)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.FullName))}")
            .ToArray();

        Assert.Empty(clashes);
    }

    private static string SchemaName(Type type)
    {
        // Each constructed generic (MistakeShare<A>, MistakeShare<B>) gets a schema of its own, named after its arguments.
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        return type.Name.Split('`')[0] + "Of" + string.Join("And", type.GetGenericArguments().Select(SchemaName));
    }

    private static void Collect(Type type, HashSet<Type> exposed)
    {
        if (type.IsArray)
        {
            Collect(type.GetElementType()!, exposed);
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Collect(argument, exposed);
            }
        }

        if (type.Assembly.GetName().Name?.StartsWith("TeachingAssistant.") != true || !exposed.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Collect(property.PropertyType, exposed);
        }
    }
}
