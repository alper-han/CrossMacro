namespace CrossMacro.Platform.Linux.Services.Runtime;

internal static class LinuxDaemonHandshakeDiagnostics
{
    public static LinuxDaemonHandshakeProbeResult Create(
        string? socketPath,
        LinuxInputCapabilityDetector.DaemonHandshakeProbeResult probeResult,
        TimeSpan timeout)
    {
        var resolvedSocketPath = socketPath ?? IpcProtocol.DefaultSocketPath;
        return probeResult.Succeeded
            ? LinuxDaemonHandshakeProbeResult.Success(resolvedSocketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                resolvedSocketPath,
                timeout,
                probeResult.Status,
                probeResult.Failure?.Message,
                probeResult.Failure);
    }
}
