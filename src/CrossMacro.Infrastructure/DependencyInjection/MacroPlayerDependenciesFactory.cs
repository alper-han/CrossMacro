namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>
/// Composes the playback runtime dependencies at the outer DI boundary.
/// </summary>
internal sealed class MacroPlayerDependenciesFactory(
    IServiceProvider serviceProvider,
    Func<IServiceProvider, IInputSimulatorPool?> simulatorPoolResolver)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly Func<IServiceProvider, IInputSimulatorPool?> _simulatorPoolResolver = simulatorPoolResolver ?? throw new ArgumentNullException(nameof(simulatorPoolResolver));

    internal MacroPlayerDependencies Create()
    {
        var positionProvider = _serviceProvider.GetRequiredService<IMousePositionProvider>();
        var dependencies = new MacroPlayerDependencies(
            positionProvider,
            new SystemPlaybackTimingService(),
            Task.Delay,
            CreateRuntimeElapsedMillisecondsProvider,
            () => new DefaultPlaybackCoordinator(positionProvider),
            () => new ButtonStateTracker(),
            () => new KeyStateTracker(),
            new DefaultPlaybackMouseButtonMapper(),
            _serviceProvider.GetService<Func<IInputSimulator>>(),
            _simulatorPoolResolver(_serviceProvider),
            _serviceProvider.GetRequiredService<IScreenPixelReader>(),
            _serviceProvider.GetRequiredService<IKeyCodeMapper>(),
            ResolveWindowManager(),
            _serviceProvider.GetService<IClipboardService>(),
            _serviceProvider.GetRequiredService<IShellCommandRunner>(),
            _serviceProvider.GetRequiredService<IScreenshotCaptureService>(),
            _serviceProvider.GetRequiredService<IImageClickMovementResolver>(),
            _serviceProvider.GetRequiredService<IImageAssetCodec>(),
            new PlaybackDelayResolver());

        return dependencies;
    }

    private IWindowManager ResolveWindowManager() =>
        _serviceProvider.GetService<IWindowManager>()
        ?? new NullWindowManager(operation =>
            Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", operation));

    private static Func<double> CreateRuntimeElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }
}
