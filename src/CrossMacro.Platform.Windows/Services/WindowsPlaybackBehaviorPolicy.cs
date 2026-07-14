using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Windows.Services;

internal sealed class WindowsPlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    public bool UseHybridAbsoluteDragMovement => false;
}
