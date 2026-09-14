
namespace CrossMacro.Platform.Linux.Services.Capabilities;

public readonly record struct LinuxInputCapabilitySnapshot(
    string? ResolvedSocketPath,
    bool DaemonSocketExists,
    bool DaemonHandshakeSucceeded,
    bool DaemonHandshakeTimedOut,
    bool CanUseDirectUInput,
    bool CanReadInputEvents,
    LinuxDaemonHandshakeProbeResult? DaemonHandshakeDiagnostic = null,
    InputProviderMode? ResolvedMode = null)
{
    /// <summary>Time of the cached daemon observation; null for legacy snapshots or disabled daemon probing.</summary>
    public DateTime? DaemonObservedAtUtc { get; init; }

    /// <summary>True when a known-good daemon was retained from socket presence without another handshake.</summary>
    public bool DaemonAvailabilityFromSocketHistory { get; init; }

    public bool HasDirectInputAccess => CanUseDirectUInput && CanReadInputEvents;

    public LinuxDaemonHandshakeProbeResult DaemonHandshake =>
        DaemonHandshakeDiagnostic ?? CreateLegacyDaemonHandshake();

    private LinuxDaemonHandshakeProbeResult CreateLegacyDaemonHandshake()
    {
        var socketPath = ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath;
        LinuxDaemonHandshakeStatus status;
        if (DaemonHandshakeSucceeded)
        {
            status = LinuxDaemonHandshakeStatus.Success;
        }
        else if (DaemonHandshakeTimedOut)
        {
            status = LinuxDaemonHandshakeStatus.Timeout;
        }
        else if (DaemonSocketExists)
        {
            status = LinuxDaemonHandshakeStatus.UnexpectedError;
        }
        else
        {
            status = LinuxDaemonHandshakeStatus.MissingSocket;
        }

        return status is LinuxDaemonHandshakeStatus.Success
            ? LinuxDaemonHandshakeProbeResult.Success(socketPath, TimeSpan.Zero)
            : LinuxDaemonHandshakeProbeResult.Failed(socketPath, TimeSpan.Zero, status);
    }
}
