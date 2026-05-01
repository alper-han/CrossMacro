namespace CrossMacro.Platform.Abstractions;

public interface IPlaybackBehaviorPolicy
{
    bool PreferRelativeForAbsoluteMoves { get; }
    bool UseHybridAbsoluteDragMovement { get; }
}
