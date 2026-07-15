namespace CrossMacro.Platform.Abstractions;


public interface ILinuxDaemonHandshakeProbe
{
    bool Probe(string socketPath);

    LinuxDaemonHandshakeProbeResult Probe(string socketPath, TimeSpan timeout);
}
