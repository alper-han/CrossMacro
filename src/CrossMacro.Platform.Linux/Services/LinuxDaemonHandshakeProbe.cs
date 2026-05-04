using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxDaemonHandshakeProbe : ILinuxDaemonHandshakeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public bool Probe(string socketPath)
    {
        var result = LinuxDaemonHandshakeTransport.ProbeWithinBudget(socketPath, ProbeTimeout);
        return result.Succeeded && !result.TimedOut;
    }
}
