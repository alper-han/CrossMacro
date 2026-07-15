using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public sealed class PlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    public PlaybackBehaviorPolicy(bool useHybridAbsoluteDragMovement)
    {
        UseHybridAbsoluteDragMovement = useHybridAbsoluteDragMovement;
    }

    public bool UseHybridAbsoluteDragMovement { get; }
}
