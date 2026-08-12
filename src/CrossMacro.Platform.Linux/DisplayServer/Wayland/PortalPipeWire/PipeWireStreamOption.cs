namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[Flags]
internal enum PipeWireStreamOption
{
    None = 0,
    Autoconnect = 1 << 0,
    Inactive = 1 << 1,
    MapBuffers = 1 << 2,
    AllocBuffers = 1 << 8,
}
