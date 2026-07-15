namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface ILinuxDaemonSocketAccessProbe
{
    public LinuxDaemonSocketAccessResult Probe(LinuxDaemonSocketProbeOptions options);
}
