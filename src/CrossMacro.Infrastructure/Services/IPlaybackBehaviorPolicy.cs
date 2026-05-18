using System;
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

public sealed class RuntimePlaybackBehaviorPolicy : IPlaybackBehaviorPolicy
{
    private readonly IRuntimeContext _runtimeContext;

    public RuntimePlaybackBehaviorPolicy(IRuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
    }

    public bool UseHybridAbsoluteDragMovement => _runtimeContext.IsLinux;
}
