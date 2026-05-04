namespace CrossMacro.Platform.Abstractions;

public interface ILinuxDaemonHandshakeProbe
{
    bool Probe(string socketPath);
}
