using Xunit;

namespace CasaEngine.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProjectEnvironmentCollection
{
    public const string Name = "ProjectEnvironment";
}