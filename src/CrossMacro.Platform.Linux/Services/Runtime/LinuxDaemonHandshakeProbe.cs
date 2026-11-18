
namespace CrossMacro.Platform.Linux.Services.Runtime;

internal sealed class LinuxDaemonHandshakeProbe : ILinuxDaemonHandshakeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private readonly Func<string, TimeSpan, CancellationToken, Task<LinuxDaemonHandshakeProbeResult>> _probe;

    public LinuxDaemonHandshakeProbe()
        : this(LinuxDaemonHandshakeTransport.ProbeAsync) { /* Empty */ }

    internal LinuxDaemonHandshakeProbe(Func<string, TimeSpan, CancellationToken, Task<LinuxDaemonHandshakeProbeResult>> probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public bool Probe(string socketPath) => Probe(socketPath, ProbeTimeout).Succeeded;

    // The abstraction is synchronous (CLI and MCP preflight run off any synchronization context); the
    // transport's awaits are all ConfigureAwait(false), so blocking here cannot deadlock.
    public LinuxDaemonHandshakeProbeResult Probe(string socketPath, TimeSpan timeout) =>
        _probe(socketPath, timeout, CancellationToken.None).GetAwaiter().GetResult();
}
