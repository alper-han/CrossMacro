namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class PlaybackRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services, Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver)
    {
        _ = services.AddSingleton<IGlobalHotkeyService>(sp => new GlobalHotkeyService(sp.GetRequiredService<IHotkeyConfigurationService>(), sp.GetRequiredService<IHotkeyParser>(), sp.GetRequiredService<IHotkeyMatcher>(), sp.GetRequiredService<IModifierStateTracker>(), sp.GetRequiredService<IHotkeyStringBuilder>(), sp.GetRequiredService<IMouseButtonMapper>(), sp.GetService<Func<IInputCapture>>()));
        _ = services.AddSingleton<IImageClickMovementResolver>(sp => new ImageClickMovementResolver(sp.GetRequiredService<IMousePositionProvider>()));
        _ = services.AddTransient<IPlaybackValidator, PlaybackValidator>();
        _ = services.AddTransient<IMacroPlayer>(sp =>
        {
            var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
            var dependencies = new MacroPlayerDependencies(positionProvider, new PlaybackTimingService(), Task.Delay, CreateRuntimeElapsedMillisecondsProvider, () => new DefaultPlaybackCoordinator(positionProvider), () => new ButtonStateTracker(), () => new KeyStateTracker(), new DefaultPlaybackMouseButtonMapper(), sp.GetService<Func<IInputSimulator>>(), simulatorPoolResolver(sp), sp.GetService<IPlaybackBehaviorPolicy>() ?? new PlaybackBehaviorPolicy(useHybridAbsoluteDragMovement: false), sp.GetRequiredService<IScreenPixelReader>(), sp.GetRequiredService<IKeyCodeMapper>(), sp.GetService<IWindowManager>() ?? new NullWindowManager(op => Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", op)), sp.GetService<IClipboardService>(), sp.GetRequiredService<IShellCommandRunner>(), sp.GetRequiredService<IScreenshotCaptureService>(), sp.GetRequiredService<IImageClickMovementResolver>(), sp.GetRequiredService<IImageAssetCodec>(), new PlaybackDelayResolver());
            return new MacroPlayer(sp.GetRequiredService<IPlaybackValidator>(), dependencies);
        });
        _ = services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());
    }

    private static Func<double> CreateRuntimeElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }
}
