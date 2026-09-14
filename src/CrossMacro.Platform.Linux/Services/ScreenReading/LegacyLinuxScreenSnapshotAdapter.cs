namespace CrossMacro.Platform.Linux.Services.ScreenReading;

/// <summary>Compatibility construction reaches the same immutable screen-selection path as DI.</summary>
internal sealed class LegacyLinuxScreenSnapshotAdapter(
    ILinuxEnvironmentDetector environment,
    IRuntimeContext runtime,
    ILinuxScreenReaderCapabilityDetector screen) : ILinuxCapabilitySnapshotProvider
{
    private readonly ILinuxEnvironmentDetector _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    private readonly IRuntimeContext _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly ILinuxScreenReaderCapabilityDetector _screen = screen ?? throw new ArgumentNullException(nameof(screen));

    public LinuxCapabilitySnapshot GetSnapshot()
    {
        var compositor = CompositorType.Unknown;
        if (_environment.IsWayland)
        {
            compositor = _environment.DetectedCompositor is CompositorType.Unknown or CompositorType.X11
                ? CompositorType.Other : _environment.DetectedCompositor;
        }
        else if (_environment.IsX11)
        {
            compositor = CompositorType.X11;
        }
        return new LinuxCapabilitySnapshot(
            default(LinuxEnvironmentSnapshot) with { FlatpakInfoExists = _runtime.IsFlatpak },
            compositor, default,
            _environment.IsWayland ? _screen.GetSnapshot() : LinuxScreenReaderCapabilitySnapshot.NotApplicable("Screen backend is not Wayland."));
    }
    public void InvalidateCache() => _screen.InvalidateCache();
    public void InvalidateScreenReadingCache() => _screen.InvalidateCache();
}
