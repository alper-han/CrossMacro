namespace CrossMacro.Platform.Abstractions;


public interface ILinuxDaemonHandshakeProbe
{
    public bool Probe(string socketPath);

    public LinuxDaemonHandshakeProbeResult Probe(string socketPath, TimeSpan timeout);
}
