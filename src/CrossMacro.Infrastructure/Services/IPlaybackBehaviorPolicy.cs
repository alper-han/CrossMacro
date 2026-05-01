using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public sealed class PlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    public PlaybackBehaviorPolicy(
        bool preferRelativeForAbsoluteMoves,
        bool useHybridAbsoluteDragMovement)
    {
        PreferRelativeForAbsoluteMoves = preferRelativeForAbsoluteMoves;
        UseHybridAbsoluteDragMovement = useHybridAbsoluteDragMovement;
    }

    public bool PreferRelativeForAbsoluteMoves { get; }
    public bool UseHybridAbsoluteDragMovement { get; }
}

public sealed class RuntimePlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    private readonly IRuntimeContext _runtimeContext;

    public RuntimePlaybackBehaviorPolicy(IRuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
    }

    public bool PreferRelativeForAbsoluteMoves => _runtimeContext.IsLinux;
    public bool UseHybridAbsoluteDragMovement => _runtimeContext.IsLinux;
}
