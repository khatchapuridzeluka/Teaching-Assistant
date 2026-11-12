using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Models;

public static class AgentLinks
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string? Write(IReadOnlyList<AgentLink> links)
    {
        return links.Count == 0 ? null : JsonSerializer.Serialize(links.DistinctBy(l => l.Key).ToList(), Options);
    }

    public static IReadOnlyList<AgentLink> Read(string? json)
    {
        return json is null ? [] : JsonSerializer.Deserialize<List<AgentLink>>(json, Options) ?? [];
    }
}
