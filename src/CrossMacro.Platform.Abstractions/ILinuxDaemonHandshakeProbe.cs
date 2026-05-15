namespace CrossMacro.Platform.Abstractions;

using System;
using CrossMacro.Platform.Abstractions.Diagnostics;

public interface ILinuxDaemonHandshakeProbe
{
    bool Probe(string socketPath);

    LinuxDaemonHandshakeProbeResult Probe(string socketPath, TimeSpan timeout);
}
