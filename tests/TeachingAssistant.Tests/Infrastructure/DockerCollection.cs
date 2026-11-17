namespace TeachingAssistant.Tests.Infrastructure;

/// <summary>Tests that start real Docker containers; xUnit runs the classes of one collection one after another.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DockerCollection
{
    public const string Name = "Docker";
}
