namespace CrossMacro.Infrastructure.Tests.TestDoubles;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalCoreLoggerGroup
{
    public const string Name = "GlobalCoreLogger";

    private GlobalCoreLoggerGroup()
    {
    }
}
