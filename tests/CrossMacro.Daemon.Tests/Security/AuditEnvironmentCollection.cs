namespace CrossMacro.Daemon.Tests.Security;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuditEnvironmentCollection
{
    private AuditEnvironmentCollection()
    {
    }

    public const string Name = "Audit environment";
}
