namespace CrossMacro.Cli.Services;

/// <summary>
/// Hosts the local Model Context Protocol session selected by the executable
/// composition root.
/// </summary>
public interface IMcpServer
{
    public Task RunAsync(CancellationToken cancellationToken, bool restricted = false);
}
