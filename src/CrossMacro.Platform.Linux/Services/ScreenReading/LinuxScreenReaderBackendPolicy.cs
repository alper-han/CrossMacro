namespace CrossMacro.Platform.Linux.Services.ScreenReading;


internal static class LinuxScreenReaderBackendPolicy
{
    private static readonly LinuxScreenReaderBackend[] NativeKdeWaylandOrder =
    [
        LinuxScreenReaderBackend.KWinScreenShot2,
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy,
        LinuxScreenReaderBackend.Portal,
    ];

    private static readonly LinuxScreenReaderBackend[] NativeWaylandOrder =
    [
        LinuxScreenReaderBackend.GnomeExtension,
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy,
        LinuxScreenReaderBackend.Portal,
    ];

    private static readonly LinuxScreenReaderBackend[] FlatpakWaylandOrder =
    [
        LinuxScreenReaderBackend.GnomeExtension,
        LinuxScreenReaderBackend.Portal,
        LinuxScreenReaderBackend.ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy,
    ];

    public static IReadOnlyList<LinuxScreenReaderBackend> GetOrder(bool isFlatpak, CompositorType compositor)
    {
        if (isFlatpak)
        {
            return FlatpakWaylandOrder;
        }
        return compositor is CompositorType.KDE ? NativeKdeWaylandOrder : NativeWaylandOrder;
    }

    public static string GetPolicyName(bool isFlatpak, CompositorType compositor)
    {
        if (isFlatpak)
        {
            return "Flatpak";
        }
        return compositor is CompositorType.KDE ? "NativeKDE" : "Native";
    }
}
