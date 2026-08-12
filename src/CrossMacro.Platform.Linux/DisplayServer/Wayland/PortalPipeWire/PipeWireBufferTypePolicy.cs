namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class PipeWireBufferTypePolicy
{
    internal const uint SpaDataMemFd = 2;
    internal const uint SpaDataDmaBuf = 3;

    public static bool IsSupported(uint dataType) => dataType is SpaDataMemFd;

    public static string DescribeUnsupported(uint dataType) => dataType switch
    {
        SpaDataDmaBuf => "PipeWire negotiated SPA_DATA_DmaBuf, but this capture path requires SPA_DATA_MemFd; a DmaBuf modifier-aware consumer is not enabled.",
        _ => $"PipeWire negotiated unsupported buffer data type {dataType.ToString(CultureInfo.InvariantCulture)}; this capture path requires SPA_DATA_MemFd.",
    };
}
