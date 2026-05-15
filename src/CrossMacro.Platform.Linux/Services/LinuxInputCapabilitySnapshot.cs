using System;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxInputCapabilitySnapshot(
    string? ResolvedSocketPath,
    bool DaemonSocketExists,
    bool DaemonHandshakeSucceeded,
    bool DaemonHandshakeTimedOut,
    bool CanUseDirectUInput,
    bool CanReadInputEvents,
    LinuxDaemonHandshakeProbeResult? DaemonHandshakeDiagnostic = null)
{
    public bool HasDirectInputAccess => CanUseDirectUInput && CanReadInputEvents;

    public LinuxDaemonHandshakeProbeResult DaemonHandshake =>
        DaemonHandshakeDiagnostic ?? CreateLegacyDaemonHandshake();

    private LinuxDaemonHandshakeProbeResult CreateLegacyDaemonHandshake()
    {
        var socketPath = ResolvedSocketPath ?? IpcProtocol.DefaultSocketPath;
        var status = DaemonHandshakeSucceeded
            ? LinuxDaemonHandshakeStatus.Success
            : DaemonHandshakeTimedOut
                ? LinuxDaemonHandshakeStatus.Timeout
                : DaemonSocketExists
                    ? LinuxDaemonHandshakeStatus.UnexpectedError
                    : LinuxDaemonHandshakeStatus.MissingSocket;

        return status == LinuxDaemonHandshakeStatus.Success
            ? LinuxDaemonHandshakeProbeResult.Success(socketPath, TimeSpan.Zero)
            : LinuxDaemonHandshakeProbeResult.Failed(socketPath, TimeSpan.Zero, status);
    }
}
