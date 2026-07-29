namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface ILinuxDaemonSocketAccessProbe
{
    public ValueTask<LinuxDaemonSocketAccessResult> ProbeAsync(LinuxDaemonSocketProbeOptions options, CancellationToken cancellationToken = default);
}
