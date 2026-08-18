namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaMeta
{
    public uint Type;
    public uint Size;
    public IntPtr Data;
}
