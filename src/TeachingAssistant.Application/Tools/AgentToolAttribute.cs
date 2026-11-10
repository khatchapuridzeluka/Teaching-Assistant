namespace TeachingAssistant.Application.Tools;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentToolAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
