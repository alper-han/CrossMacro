namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[Flags]
internal enum PipeWireStreamOption
{
    Autoconnect = 1 << 0,
    MapBuffers = 1 << 2,
    AllocBuffers = 1 << 8,
}
