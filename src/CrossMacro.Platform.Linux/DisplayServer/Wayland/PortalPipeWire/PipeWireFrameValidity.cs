namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class PipeWireFrameValidity
{
    private const uint MaxMetadataCount = 64;

    public static bool IsUsable(SpaBuffer buffer, SpaChunk chunk, out PipeWireFrameDropReason reason)
    {
        if ((chunk.Flags & PipeWireConstants.SpaChunkFlagCorrupted) is not 0)
        {
            reason = PipeWireFrameDropReason.CorruptedChunk;
            return false;
        }

        if ((chunk.Flags & PipeWireConstants.SpaChunkFlagEmpty) is not 0)
        {
            reason = PipeWireFrameDropReason.EmptyChunk;
            return false;
        }

        if (TryReadHeaderFlags(buffer, out var headerFlags) &&
            (headerFlags & PipeWireConstants.SpaMetaHeaderFlagCorrupted) is not 0)
        {
            reason = PipeWireFrameDropReason.CorruptedHeader;
            return false;
        }

        if (chunk.Size is 0)
        {
            reason = PipeWireFrameDropReason.EmptyPayload;
            return false;
        }

        reason = PipeWireFrameDropReason.None;
        return true;
    }

    private static bool TryReadHeaderFlags(SpaBuffer buffer, out uint flags)
    {
        flags = 0;
        if (buffer.MetaCount is 0 || buffer.Metas == IntPtr.Zero)
        {
            return false;
        }

        var metadataCount = Math.Min(buffer.MetaCount, MaxMetadataCount);
        var metadataSize = Marshal.SizeOf<SpaMeta>();
        for (var index = 0u; index < metadataCount; index++)
        {
            var metadata = Marshal.PtrToStructure<SpaMeta>(IntPtr.Add(buffer.Metas, checked((int)(index * (uint)metadataSize))));
            if (metadata.Type != PipeWireConstants.SpaMetaHeader ||
                metadata.Data == IntPtr.Zero ||
                metadata.Size < sizeof(uint))
            {
                continue;
            }

            flags = unchecked((uint)Marshal.ReadInt32(metadata.Data));
            return true;
        }

        return false;
    }
}
