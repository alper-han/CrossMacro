
namespace CrossMacro.Infrastructure.Services;

public sealed class PlaybackBehaviorPolicy(bool useHybridAbsoluteDragMovement) : IPlaybackBehaviorPolicy
{
    public bool UseHybridAbsoluteDragMovement { get; } = useHybridAbsoluteDragMovement;
}
