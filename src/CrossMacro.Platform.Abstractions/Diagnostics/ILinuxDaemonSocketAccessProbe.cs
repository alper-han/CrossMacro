namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface ILinuxDaemonSocketAccessProbe
{
    LinuxDaemonSocketAccessResult Probe(LinuxDaemonSocketProbeOptions options);
}
