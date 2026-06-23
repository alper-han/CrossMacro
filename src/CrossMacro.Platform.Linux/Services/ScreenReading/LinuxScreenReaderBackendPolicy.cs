namespace CrossMacro.Platform.Linux.Services.ScreenReading;

using CrossMacro.Platform.Linux.DisplayServer;

internal static class LinuxScreenReaderBackendPolicy
{
    private static readonly LinuxScreenReaderBackend[] NativeKdeWaylandOrder =
    [
        LinuxScreenReaderBackend.KWinScreenShot2,
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy,
        LinuxScreenReaderBackend.Portal
    ];

    private static readonly LinuxScreenReaderBackend[] NativeWaylandOrder =
    [
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy,
        LinuxScreenReaderBackend.Portal
    ];

    private static readonly LinuxScreenReaderBackend[] FlatpakWaylandOrder =
    [
        LinuxScreenReaderBackend.Portal,
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy
    ];

    public static IReadOnlyList<LinuxScreenReaderBackend> GetOrder(bool isFlatpak, CompositorType compositor) =>
        isFlatpak ? FlatpakWaylandOrder : compositor == CompositorType.KDE ? NativeKdeWaylandOrder : NativeWaylandOrder;

    public static string GetPolicyName(bool isFlatpak, CompositorType compositor) =>
        isFlatpak ? "Flatpak" : compositor == CompositorType.KDE ? "NativeKDE" : "Native";
}
