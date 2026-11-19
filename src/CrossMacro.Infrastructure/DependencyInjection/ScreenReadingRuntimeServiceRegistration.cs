using CrossMacro.Infrastructure.Services.ScreenCapture;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class ScreenReadingRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IImageAssetCodec, ImageAssetCodec>();
        services.TryAddSingleton<IImageAssetPreviewDecoder, ImageAssetPreviewDecoder>();
        services.TryAddSingleton<IScreenFrameProvider, UnsupportedScreenFrameProvider>();
        services.TryAddSingleton<IScreenshotCaptureService>(sp => new ScreenshotCaptureService(sp.GetService<IScreenFrameProvider>(), sp.GetService<IImageClipboardService>(), sp.GetRequiredService<IImageAssetCodec>()));
        services.TryAddSingleton<IScreenImageSearchReader, ScreenPixelReader>();
        services.TryAddSingleton<IScreenPixelReader>(sp => sp.GetRequiredService<IScreenImageSearchReader>());
        _ = services.AddSingleton<IImageClickMovementResolver>(sp => new ImageClickMovementResolver(sp.GetRequiredService<IMousePositionProvider>()));
        services.TryAddSingleton<IScreenImageAutomation>(sp => new ScreenImageAutomation(sp.GetRequiredService<IScreenImageSearchReader>(), sp.GetRequiredService<IImageAssetCodec>(), sp.GetService<IMousePositionProvider>(), sp.GetService<Func<IInputSimulator>>(), sp.GetService<IInputSimulatorPool>(), sp.GetRequiredService<IImageClickMovementResolver>(), sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IScreenReadingWarmupService>(sp => new ScreenReadingWarmupService(
            sp.GetRequiredService<IScreenFrameProvider>(),
            sp.GetService<IScreenReadingDiagnosticProvider>(),
            sp.GetService<IScreenReadingCapabilityReadiness>()));
    }
}
