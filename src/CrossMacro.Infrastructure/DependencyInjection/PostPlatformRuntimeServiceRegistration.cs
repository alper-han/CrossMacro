namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class PostPlatformRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        _ = services.AddSingleton<Func<IKeyCodeMapper>>(sp => sp.GetRequiredService<IKeyCodeMapper>);
        _ = services.AddSingleton<IMouseButtonMapper, MouseButtonMapper>();
        _ = services.AddSingleton<IModifierStateTracker, ModifierStateTracker>();
        _ = services.AddSingleton<IHotkeyParser, HotkeyParser>();
        _ = services.AddSingleton<IHotkeyStringBuilder, HotkeyStringBuilder>();
        _ = services.AddSingleton<IHotkeyMatcher, HotkeyMatcher>();
        _ = services.AddSingleton<IMacroFileManager, MacroFileManager>();
        _ = services.AddSingleton<IScriptValidationService, ScriptValidationService>();
        _ = services.AddSingleton<IRunExecutionService>(sp => new RunScriptRuntimeService(sp.GetRequiredService<Func<IMacroPlayer>>(), sp.GetRequiredService<IKeyCodeMapper>(), sp.GetService<IMousePositionProvider>()));
        services.TryAddSingleton<IImageAssetCodec, ImageAssetCodec>();
        services.TryAddSingleton<IImageAssetPreviewDecoder, ImageAssetPreviewDecoder>();
        services.TryAddSingleton<IScreenFrameProvider, UnsupportedScreenFrameProvider>();
        services.TryAddSingleton<IScreenshotCaptureService>(sp => new ScreenshotCaptureService(sp.GetService<IScreenFrameProvider>(), sp.GetService<IImageClipboardService>(), sp.GetRequiredService<IImageAssetCodec>()));
        services.TryAddSingleton<IScreenPixelReader, ScreenPixelReader>();
        services.TryAddSingleton<IScreenImageAutomation>(sp => new ScreenImageAutomation(sp.GetRequiredService<IScreenPixelReader>(), sp.GetRequiredService<IImageAssetCodec>(), sp.GetService<IMousePositionProvider>(), sp.GetService<Func<IInputSimulator>>(), sp.GetService<IInputSimulatorPool>(), sp.GetRequiredService<IImageClickMovementResolver>(), sp.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IScreenReadingWarmupService>(sp => new ScreenReadingWarmupService(
            sp.GetRequiredService<IScreenFrameProvider>(),
            sp.GetService<IScreenReadingDiagnosticProvider>(),
            sp.GetService<IScreenReadingCapabilityReadiness>()));
    }
}
