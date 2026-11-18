namespace CrossMacro.Infrastructure.Tests.TestDoubles;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalCoreLoggerCollection
{
    public const string Name = "GlobalCoreLogger";

    private GlobalCoreLoggerCollection()
    {
    }
}
