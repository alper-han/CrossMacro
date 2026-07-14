using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxPlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    public bool UseHybridAbsoluteDragMovement => true;
}
