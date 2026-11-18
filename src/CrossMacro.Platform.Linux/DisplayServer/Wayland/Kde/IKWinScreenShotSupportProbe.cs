
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

public interface IKWinScreenShotSupportProbe
{
    public WaylandSupportResult ProbeSupport();

    /// <summary>
    /// Asynchronously checks KWin availability. Existing implementations retain the
    /// synchronous member as a compatibility fallback while KWin's production probe
    /// provides true asynchronous I/O and cancellation.
    /// </summary>
    public Task<WaylandSupportResult> ProbeSupportAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProbeSupport());
    }
}
