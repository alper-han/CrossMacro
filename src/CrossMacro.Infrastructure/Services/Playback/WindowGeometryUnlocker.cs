
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class WindowGeometryUnlocker
{
    public static async Task UnlockAsync(IWindowQueryService query, IWindowMutationService mutator, CancellationToken cancellationToken)
    {
        var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        if (info != null)
        {
            bool stateChanged = false;
            if (info.IsFullscreen)
            {
                _ = await mutator.FullscreenActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (info.IsMaximized)
            {
                _ = await mutator.MaximizeActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (!info.IsFloating)
            {
                _ = await mutator.FloatActiveWindowAsync(cancellationToken).ConfigureAwait(false);
                stateChanged = true;
            }
            if (stateChanged)
            {
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
