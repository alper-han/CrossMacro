
namespace CrossMacro.UI.Services;

internal static class InputSimulatorWarmupService
{
    public static async Task WarmUpAsync(
        IInputSimulatorPool simulatorPool,
        IMousePositionProvider? positionProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(simulatorPool);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var width = 0;
            var height = 0;

            if (positionProvider is not null)
            {
                var resolution = await positionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (resolution is not null)
                {
                    width = resolution.Value.Width;
                    height = resolution.Value.Height;
                }
            }

            await simulatorPool.WarmUpAsync(width, height).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[DesktopStartupCoordinator] Input simulator warm-up skipped: {Error}", ex.Message);
                return;
            }

            Log.LogError(ex, "[DesktopStartupCoordinator] Failed to warm up InputSimulatorPool");
        }
    }
}
