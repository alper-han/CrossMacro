using System;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxDaemonHandshakeProbe : ILinuxDaemonHandshakeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private readonly Func<string, TimeSpan, LinuxDaemonHandshakeTransport.ProbeResult> _probeWithinBudget;

    public LinuxDaemonHandshakeProbe()
        : this(LinuxDaemonHandshakeTransport.ProbeWithinBudget)
    {
    }

    internal LinuxDaemonHandshakeProbe(Func<string, TimeSpan, LinuxDaemonHandshakeTransport.ProbeResult>? probeWithinBudget = null)
    {
        _probeWithinBudget = probeWithinBudget ?? LinuxDaemonHandshakeTransport.ProbeWithinBudget;
    }

    public bool Probe(string socketPath)
    {
        return Probe(socketPath, ProbeTimeout).Succeeded;
    }

    public LinuxDaemonHandshakeProbeResult Probe(string socketPath, TimeSpan timeout)
    {
        return LinuxInputProbeUtilities.MapDaemonHandshakeTransportResult(
            socketPath,
            timeout,
            _probeWithinBudget(socketPath, timeout));
    }
}
