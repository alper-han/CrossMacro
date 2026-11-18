using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Input.Simulation;

namespace CrossMacro.UI.Services.Runtime;

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
                var resolution = await positionProvider
                    .GetScreenResolutionAsync()
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (resolution is not null)
                {
                    width = resolution.Value.Width;
                    height = resolution.Value.Height;
                }
            }

            await simulatorPool.WarmUpAsync(width, height, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug("[InputSimulatorWarmupService] Input simulator warm-up cancelled during shutdown");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[InputSimulatorWarmupService] Input simulator warm-up skipped: {Error}", ex.Message);
                return;
            }

            Log.LogError(ex, "[InputSimulatorWarmupService] Failed to warm up InputSimulatorPool");
        }
    }
}
