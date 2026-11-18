
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class WindowGeometryUnlocker
{
    public static async Task UnlockAsync(
        IWindowManager manager,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        var info = await manager.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        if (info != null)
        {
            bool stateChanged = false;
            if (info.IsFullscreen)
            {
                _ = await manager.FullscreenActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (info.IsMaximized)
            {
                _ = await manager.MaximizeActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (!info.IsFloating)
            {
                _ = await manager.FloatActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (stateChanged)
            {
                await (delayAsync ?? ((delay, token) => Task.Delay(delay, TimeProvider.System, token)))
                    (GeometrySettleDelay, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
