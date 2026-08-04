
namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IMousePositionProvider
/// based on the detected Linux desktop environment.
/// Single Responsibility: Only handles position provider creation.
/// </summary>
public class LinuxPositionProviderFactory
{
    private readonly IEnumerable<IPositionProviderSelector> _selectors;
    private readonly ILinuxEnvironmentDetector? _environmentDetector;
    private readonly ILinuxCapabilitySnapshotProvider? _snapshotProvider;
    private readonly Func<IMousePositionProvider?>? _waylandCursorProviderFactory;

    public LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<IMousePositionProvider?>? waylandCursorProviderFactory = null)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = null;
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _waylandCursorProviderFactory = waylandCursorProviderFactory;
    }

    internal LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxEnvironmentDetector environmentDetector)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _snapshotProvider = null;
        _waylandCursorProviderFactory = null;
    }

    internal LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxCapabilitySnapshotProvider snapshotProvider)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _waylandCursorProviderFactory = null;
    }

    /// <summary>
    /// Creates the appropriate position provider for the current desktop environment.
    /// </summary>
    public IMousePositionProvider Create()
    {
        var snapshot = _snapshotProvider?.GetSnapshot();
        var compositorType = snapshot?.Compositor ?? _environmentDetector?.DetectedCompositor ?? CompositorType.Unknown;

        LoggingExtensions.LogOnce("LinuxPositionProviderFactory_Compositor", "[LinuxPositionProviderFactory] Compositor: {Compositor}", compositorType);

        var provider = _selectors
            .Where(s => s.CanHandle(compositorType))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault()
            ?.Create();

        if (provider is null)
        {
            Log.Warning("[LinuxPositionProviderFactory] No matching selector found for {Compositor}, using Fallback.", compositorType);
            provider = new FallbackPositionProvider();
        }

        if (snapshot is { IsWayland: true }
            && provider is not IMousePositionChangeSource
            && _waylandCursorProviderFactory?.Invoke() is { } waylandCursorProvider)
        {
            Log.Information(
                "[LinuxPositionProviderFactory] Using ext-image-copy cursor notifications with {FallbackProvider} fallback",
                provider.ProviderName);
            return new CompositeMousePositionProvider(waylandCursorProvider, provider);
        }

        return provider;
    }
}
