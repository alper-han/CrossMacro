namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal enum PipeWireDirection
{
    Input = 0
}

[Flags]
internal enum PipeWireStreamFlags
{
    Autoconnect = 1 << 0,
    MapBuffers = 1 << 2,
    AllocBuffers = 1 << 8
}

internal static class PipeWireConstants
{
    public const uint PwIdAny = 0xffffffff;
    public const int Xrgb8888BytesPerPixel = 4;
}
