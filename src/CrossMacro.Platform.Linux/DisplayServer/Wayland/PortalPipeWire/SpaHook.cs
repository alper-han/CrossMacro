
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaHook
{
    public SpaList Link;
    public SpaCallbacks Callbacks;
    public IntPtr Removed;
    public IntPtr Private;
}
