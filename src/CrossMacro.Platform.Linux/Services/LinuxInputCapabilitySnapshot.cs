using System;

namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxInputCapabilitySnapshot(
    string? ResolvedSocketPath,
    bool DaemonSocketExists,
    bool DaemonHandshakeSucceeded,
    bool DaemonHandshakeTimedOut,
    bool CanUseDirectUInput,
    bool CanReadInputEvents)
{
    public bool HasDirectInputAccess => CanUseDirectUInput && CanReadInputEvents;
}
