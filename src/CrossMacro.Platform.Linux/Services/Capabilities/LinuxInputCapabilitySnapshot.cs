
namespace CrossMacro.Platform.Linux.Services.Capabilities;

public readonly record struct LinuxInputCapabilitySnapshot(
    string? ResolvedSocketPath,
    bool CanUseDirectUInput,
    bool CanReadInputEvents,
    LinuxDaemonHandshakeProbeResult? DaemonHandshakeDiagnostic = null,
    InputProviderMode? ResolvedMode = null)
{
    /// <summary>Time of the cached daemon observation; null when daemon probing is disabled.</summary>
    public DateTime? DaemonObservedAtUtc { get; init; }

    /// <summary>True when a known-good daemon was retained from socket presence without another handshake.</summary>
    public bool DaemonAvailabilityFromSocketHistory { get; init; }

    public bool HasDirectInputAccess => CanUseDirectUInput && CanReadInputEvents;

    /// <summary>The daemon observation; a snapshot without one reports the daemon socket as missing.</summary>
    public LinuxDaemonHandshakeProbeResult DaemonHandshake =>
        DaemonHandshakeDiagnostic ?? LinuxDaemonHandshakeProbeResult.Failed(
            ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath,
            TimeSpan.Zero,
            LinuxDaemonHandshakeStatus.MissingSocket);
}
