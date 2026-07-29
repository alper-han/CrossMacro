
namespace CrossMacro.Infrastructure.Services;

public sealed class RuntimePlaybackBehaviorPolicy(IRuntimeContext runtimeContext) : IPlaybackBehaviorPolicy
{
    private readonly IRuntimeContext _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));

    public bool UseHybridAbsoluteDragMovement => _runtimeContext.IsLinux;
}
