using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.UI.Services;

internal sealed class InputSimulatorWarmupService
{
    public async Task WarmUpAsync(
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

            if (positionProvider != null)
            {
                var resolution = await positionProvider.GetScreenResolutionAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (resolution.HasValue)
                {
                    width = resolution.Value.Width;
                    height = resolution.Value.Height;
                }
            }

            await simulatorPool.WarmUpAsync(width, height);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[DesktopStartupCoordinator] Input simulator warm-up skipped: {Error}", ex.Message);
                return;
            }

            Log.Error(ex, "[DesktopStartupCoordinator] Failed to warm up InputSimulatorPool");
        }
    }
}
