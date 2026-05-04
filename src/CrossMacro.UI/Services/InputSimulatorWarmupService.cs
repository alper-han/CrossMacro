using System;
using System.Threading.Tasks;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;

namespace CrossMacro.UI.Services;

internal sealed class InputSimulatorWarmupService
{
    public async Task WarmUpAsync(
        InputSimulatorPool simulatorPool,
        IMousePositionProvider? positionProvider)
    {
        ArgumentNullException.ThrowIfNull(simulatorPool);

        try
        {
            var width = 0;
            var height = 0;

            if (positionProvider != null)
            {
                var resolution = await positionProvider.GetScreenResolutionAsync();
                if (resolution.HasValue)
                {
                    width = resolution.Value.Width;
                    height = resolution.Value.Height;
                }
            }

            await simulatorPool.WarmUpAsync(width, height);
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
