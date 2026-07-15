using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// One immutable view of the Linux capabilities used by backend selection.
/// Probes are performed by the provider; consumers only inspect this value.
/// </summary>
public readonly record struct LinuxCapabilitySnapshot(
    LinuxEnvironmentSnapshot Environment,
    CompositorType Compositor,
    LinuxInputCapabilitySnapshot Input,
    LinuxScreenReaderCapabilitySnapshot ScreenReading)
{
    public bool IsFlatpak => Environment.IsFlatpak;

    public bool IsWayland => Compositor is not CompositorType.X11 and not CompositorType.Unknown;

    public bool IsX11 => Compositor is CompositorType.X11;

    public bool HasPortalAndPipeWire => ScreenReading.Portal.IsAvailable;

    public bool HasNativeScreenReadingFallback =>
        ScreenReading.ExtImageCopy.IsAvailable ||
        ScreenReading.WlrScreencopy.IsAvailable ||
        ScreenReading.KWinScreenShot2.IsAvailable ||
        ScreenReading.GnomeExtension.IsAvailable;
}
