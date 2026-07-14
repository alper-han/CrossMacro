using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSPlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    public bool UseHybridAbsoluteDragMovement => false;
}
