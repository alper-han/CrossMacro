namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal enum PipeWireFrameDropReason
{
    None,
    CorruptedChunk,
    EmptyChunk,
    CorruptedHeader,
    EmptyPayload,
}
