namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Creates the <see cref="IMousePositionProvider"/> for the detected compositor. On Wayland a provider that cannot
/// publish position changes is paired with the native cursor protocol provider when one is available.
/// </summary>
public sealed class LinuxPositionProviderFactory(
    ILinuxCapabilitySnapshotProvider snapshotProvider,
    GnomePositionProvider gnomeProvider,
    KdePositionProvider kdeProvider,
    Func<IMousePositionProvider?>? waylandCursorProviderFactory = null)
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider =
        snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly GnomePositionProvider _gnomeProvider =
        gnomeProvider ?? throw new ArgumentNullException(nameof(gnomeProvider));
    private readonly KdePositionProvider _kdeProvider =
        kdeProvider ?? throw new ArgumentNullException(nameof(kdeProvider));

    public IMousePositionProvider Create()
    {
        var snapshot = _snapshotProvider.GetSnapshot();
        var compositorType = snapshot.Compositor;

        LoggingExtensions.LogOnce(
            "LinuxPositionProviderFactory_Compositor",
            "[LinuxPositionProviderFactory] Compositor: {Compositor}",
            compositorType);

        var provider = CreateCompositorProvider(compositorType);
        if (provider is null)
        {
            Log.Warning("[LinuxPositionProviderFactory] No position provider for {Compositor}, using Fallback.", compositorType);
            provider = new FallbackPositionProvider();
        }

        if (snapshot is { IsWayland: true }
            && provider is not IMousePositionChangeSource
            && waylandCursorProviderFactory?.Invoke() is { } waylandCursorProvider)
        {
            Log.Information(
                "[LinuxPositionProviderFactory] Using ext-image-copy cursor notifications with {FallbackProvider} fallback",
                provider.ProviderName);
            return new CompositeMousePositionProvider(waylandCursorProvider, provider);
        }

        return provider;
    }

    private IMousePositionProvider? CreateCompositorProvider(CompositorType compositor) => compositor switch
    {
        CompositorType.X11 => new X11PositionProvider(),
        CompositorType.GNOME => _gnomeProvider,
        CompositorType.KDE => _kdeProvider,
        CompositorType.HYPRLAND => new HyprlandPositionProvider(),
        CompositorType.WAYFIRE => new WayfirePositionProvider(),
        CompositorType.NIRI => new NiriPositionProvider(),
        CompositorType.COSMIC => new CosmicPositionProvider(),
        CompositorType.SWAY => new SwayPositionProvider(),
        CompositorType.Unknown or CompositorType.Other => null,
        _ => null,
    };
}
