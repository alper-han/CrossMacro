namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct PipeWireCoreEvents
{
    public uint Version;
    public IntPtr Info;
    public IntPtr Done;
    public IntPtr Ping;
    public IntPtr Error;
    public IntPtr RemoveId;
    public IntPtr BoundId;
    public IntPtr AddMemory;
    public IntPtr RemoveMemory;
    public IntPtr BoundProperties;
}
