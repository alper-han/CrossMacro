namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>
/// Composes the playback runtime dependencies at the outer DI boundary.
/// </summary>
internal static class MacroPlayerDependenciesFactory
{
    internal static MacroPlayerDependencies Create(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver)
    {
        var positionProvider = serviceProvider.GetRequiredService<IMousePositionProvider>();
        var dependencies = new MacroPlayerDependencies(
            positionProvider,
            new SystemPlaybackTimingService(),
            Task.Delay,
            CreateRuntimeElapsedMillisecondsProvider,
            () => new DefaultPlaybackCoordinator(positionProvider),
            () => new ButtonStateTracker(),
            () => new KeyStateTracker(),
            new DefaultPlaybackMouseButtonMapper(),
            serviceProvider.GetService<Func<IInputSimulator>>(),
            simulatorPoolResolver(serviceProvider),
            serviceProvider.GetRequiredService<IScreenPixelReader>(),
            serviceProvider.GetRequiredService<IKeyCodeMapper>(),
            ResolveWindowManager(serviceProvider),
            serviceProvider.GetService<IClipboardService>(),
            serviceProvider.GetRequiredService<IShellCommandRunner>(),
            serviceProvider.GetRequiredService<IScreenshotCaptureService>(),
            serviceProvider.GetRequiredService<IImageClickMovementResolver>(),
            serviceProvider.GetRequiredService<IImageAssetCodec>(),
            new PlaybackDelayResolver());

        return dependencies;
    }

    private static IWindowManager ResolveWindowManager(IServiceProvider serviceProvider) =>
        serviceProvider.GetService<IWindowManager>()
        ?? new NullWindowManager(operation =>
            Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", operation));

    private static Func<double> CreateRuntimeElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }
}
