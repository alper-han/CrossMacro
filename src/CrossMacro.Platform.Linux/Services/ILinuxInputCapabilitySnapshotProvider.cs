using System;

namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxInputCapabilitySnapshotProvider
{
    LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget);
}
