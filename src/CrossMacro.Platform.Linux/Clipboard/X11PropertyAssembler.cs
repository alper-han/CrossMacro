namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>Reassembles a window property that the server returns in several XGetWindowProperty replies.</summary>
internal static class X11PropertyAssembler
{
    /// <summary>XGetWindowProperty offsets and lengths are counted in 32-bit units.</summary>
    internal const int WireUnitBytes = 4;

    /// <summary>Upper bound for one property, so a hostile owner cannot exhaust memory.</summary>
    internal const int MaxPropertyBytes = 256 * 1024 * 1024;

    /// <summary>Returns the Xlib byte size of one item of the given property format.</summary>
    internal static int ItemBytes(int format) => format switch
    {
        8 => sizeof(byte),
        16 => sizeof(short),
        32 => nint.Size,
        _ => throw new InvalidOperationException($"Unsupported X11 property format {format.ToString(CultureInfo.InvariantCulture)}."),
    };

    /// <summary>
    /// Reads the whole property by requesting consecutive 32-bit-unit offsets until the server reports
    /// that nothing remains after the reply.
    /// </summary>
    /// <param name="readChunk">Reads the reply that starts at the given offset, in 32-bit units.</param>
    /// <param name="maxBytes">The most data to accept before failing.</param>
    internal static X11PropertyData ReadAll(Func<long, X11PropertyChunk> readChunk, int maxBytes = MaxPropertyBytes)
    {
        ArgumentNullException.ThrowIfNull(readChunk);

        var first = readChunk(0);
        if (first.BytesAfter <= 0)
        {
            return new X11PropertyData(first.Type, first.Bytes);
        }

        var data = new ArrayBufferWriter<byte>(first.Bytes.Length + checked((int)Math.Min(first.BytesAfter, maxBytes)));
        data.Write(first.Bytes);
        var chunk = first;
        long offsetUnits = 0;
        while (chunk.BytesAfter > 0)
        {
            if (chunk.WireBytesRead <= 0)
            {
                throw new InvalidOperationException("The X11 server returned no data for a property that reports more to read.");
            }

            offsetUnits += chunk.WireBytesRead / WireUnitBytes;
            chunk = readChunk(offsetUnits);
            if (data.WrittenCount + (long)chunk.Bytes.Length > maxBytes)
            {
                throw new InvalidOperationException(
                    $"The X11 clipboard property exceeds the {maxBytes.ToString(CultureInfo.InvariantCulture)} byte limit.");
            }

            data.Write(chunk.Bytes);
        }

        return new X11PropertyData(first.Type, data.WrittenSpan.ToArray());
    }
}
