
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

/// <summary>
/// <c>struct spa_interface</c>: the leading member of every PipeWire proxy such as <c>pw_core</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SpaInterface
{
    public IntPtr Type;
    public uint Version;
    public SpaCallbacks Callbacks;
}
