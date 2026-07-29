
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

    public LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxCapabilitySnapshotProvider snapshotProvider)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = null;
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    }

    internal LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxEnvironmentDetector environmentDetector)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _snapshotProvider = null;
    }

    internal LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxEnvironmentDetector environmentDetector,
        ILinuxCapabilitySnapshotProvider snapshotProvider)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    }

    /// <summary>
    /// Creates the appropriate position provider for the current desktop environment.
    /// </summary>
    public IMousePositionProvider Create()
    {
        var compositorType = _snapshotProvider?.GetSnapshot().Compositor ?? _environmentDetector?.DetectedCompositor ?? CompositorType.Unknown;

        LoggingExtensions.LogOnce("LinuxPositionProviderFactory_Compositor", "[LinuxPositionProviderFactory] Compositor: {Compositor}", compositorType);

        var provider = _selectors
            .Where(s => s.CanHandle(compositorType))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault()
            ?.Create();

        if (provider is null)
        {
            // Should ideally not happen if Fallback selector is registered, but as a safety net:
            Log.Warning("[LinuxPositionProviderFactory] No matching selector found for {Compositor}, using Fallback.", compositorType);
            return new FallbackPositionProvider();
        }

        return provider;
    }
}
