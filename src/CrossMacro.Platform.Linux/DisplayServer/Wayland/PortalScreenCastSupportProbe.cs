using CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastSupportProbe : IPortalScreenCastSupportProbe
{
    public static PortalScreenCastSupportProbe Instance { get; } = new();

    private PortalScreenCastSupportProbe()
    {
    }

    public PortalScreenCastSupportResult ProbeSupport()
    {
        if (string.IsNullOrWhiteSpace(Tmds.DBus.Protocol.DBusAddress.Session))
        {
            return PortalScreenCastSupportResult.Unsupported("D-Bus session bus is unavailable; XDG Desktop Portal ScreenCast requires a session bus.");
        }

        return PortalPipeWireFrameCaptureFactory.CanLoadPipeWire()
            ? PortalScreenCastSupportResult.Supported()
            : PortalScreenCastSupportResult.Unsupported("libpipewire-0.3 is unavailable; XDG Desktop Portal ScreenCast requires PipeWire.");
    }
}
