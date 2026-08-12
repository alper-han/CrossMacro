namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class PipeWireConstants
{
    public const uint PwIdAny = 0xffffffff;
    public const uint SpaTypeObjectParamMeta = 0x40005;
    public const uint SpaParamEnumFormat = 3;
    public const uint SpaParamFormat = 4;
    public const uint SpaParamBuffers = 5;
    public const uint SpaParamMeta = 6;
    public const uint SpaParamMetaType = 1;
    public const uint SpaParamMetaSize = 2;
    public const uint SpaMetaHeader = 1;
    public const uint SpaMetaVideoDamage = 3;
    public const uint SpaMetaVideoTransform = 8;
    public const uint SpaChunkFlagCorrupted = 1u << 0;
    public const uint SpaChunkFlagEmpty = 1u << 1;
    public const uint SpaMetaHeaderFlagCorrupted = 1u << 1;
    public const int Xrgb8888BytesPerPixel = 4;
}
