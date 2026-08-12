namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class SpaFormatPodParser
{
    private const uint SpaTypeObject = 15;
    private const uint SpaTypeId = 3;
    private const uint SpaTypeRectangle = 10;
    private const uint SpaTypeChoice = 19;
    private const uint SpaTypeObjectFormat = 0x40003;
    private const uint SpaMediaTypeVideo = 2;
    private const uint SpaMediaSubtypeRaw = 1;
    private const uint SpaFormatMediaType = 1;
    private const uint SpaFormatMediaSubtype = 2;
    private const uint SpaFormatVideoFormat = 0x20001;
    private const uint SpaFormatVideoSize = 0x20003;
    private const uint SpaFormatVideoColorRange = 0x2000C;
    private const uint SpaFormatVideoColorMatrix = 0x2000D;
    private const uint SpaFormatVideoTransferFunction = 0x2000E;
    private const uint SpaFormatVideoColorPrimaries = 0x2000F;
    private const int HeaderSize = 8;
    private const int ObjectHeaderSize = 8;
    private const int PropertyHeaderSize = 16;
    private const int MaxPodSize = 64 * 1024;

    public static bool TryReadFormat(IntPtr parameter, out PipeWireVideoLayout layout, out string error)
    {
        layout = default;
        error = string.Empty;
        if (parameter == IntPtr.Zero)
        {
            error = "PipeWire returned an empty negotiated format pod.";
            return false;
        }

        var podSize = ReadUInt32(parameter);
        if (podSize is < (uint)ObjectHeaderSize or > (uint)(MaxPodSize - HeaderSize))
        {
            error = $"PipeWire negotiated format pod size {podSize.ToString(CultureInfo.InvariantCulture)} is invalid.";
            return false;
        }

        var podType = ReadUInt32(parameter, 4);
        if (podType != SpaTypeObject)
        {
            error = $"PipeWire negotiated format pod type {podType.ToString(CultureInfo.InvariantCulture)} is not an object.";
            return false;
        }

        if (ReadUInt32(parameter, HeaderSize) != SpaTypeObjectFormat)
        {
            error = "PipeWire negotiated pod is not a raw video format object.";
            return false;
        }

        var totalSize = checked((int)podSize + HeaderSize);
        var offset = HeaderSize + ObjectHeaderSize;
        uint formatId = 0;
        var hasFormat = false;
        var width = 0;
        var height = 0;
        var hasSize = false;
        var hasVideoMediaType = false;
        var hasRawMediaSubtype = false;
        uint? colorRange = null;
        uint? colorMatrix = null;
        uint? transferFunction = null;
        uint? colorPrimaries = null;
        var propertyKeys = new List<uint>();

        while (offset < totalSize)
        {
            if (!TryReadProperty(parameter, totalSize, ref offset, out var key, out var valueType, out var valueSize, out var valueOffset))
            {
                error = "PipeWire negotiated format pod contains a truncated property.";
                return false;
            }

            propertyKeys.Add(key);

            if (key == SpaFormatMediaType && TryReadId(parameter, valueType, valueSize, valueOffset, out var mediaType))
            {
                hasVideoMediaType = mediaType == SpaMediaTypeVideo;
            }
            else if (key == SpaFormatMediaSubtype && TryReadId(parameter, valueType, valueSize, valueOffset, out var mediaSubtype))
            {
                hasRawMediaSubtype = mediaSubtype == SpaMediaSubtypeRaw;
            }
            else if (key == SpaFormatVideoFormat && TryReadId(parameter, valueType, valueSize, valueOffset, out formatId))
            {
                hasFormat = true;
            }
            else if (key == SpaFormatVideoSize && TryReadRectangle(parameter, valueType, valueSize, valueOffset, out width, out height))
            {
                hasSize = true;
            }
            else if (key == SpaFormatVideoColorRange && TryReadId(parameter, valueType, valueSize, valueOffset, out var range))
            {
                colorRange = range;
            }
            else if (key == SpaFormatVideoColorMatrix && TryReadId(parameter, valueType, valueSize, valueOffset, out var matrix))
            {
                colorMatrix = matrix;
            }
            else if (key == SpaFormatVideoTransferFunction && TryReadId(parameter, valueType, valueSize, valueOffset, out var transfer))
            {
                transferFunction = transfer;
            }
            else if (key == SpaFormatVideoColorPrimaries && TryReadId(parameter, valueType, valueSize, valueOffset, out var primaries))
            {
                colorPrimaries = primaries;
            }
        }

        if (!hasVideoMediaType || !hasRawMediaSubtype || !hasFormat || !hasSize)
        {
            error = $"PipeWire negotiated format was not a concrete raw video format with dimensions. mediaType={hasVideoMediaType.ToString(CultureInfo.InvariantCulture)} subtype={hasRawMediaSubtype.ToString(CultureInfo.InvariantCulture)} format={hasFormat.ToString(CultureInfo.InvariantCulture)} size={hasSize.ToString(CultureInfo.InvariantCulture)} keys={string.Join(',', propertyKeys.Select(static key => key.ToString(CultureInfo.InvariantCulture)))}.";
            return false;
        }

        if (width <= 0 || height <= 0)
        {
            error = "PipeWire negotiated video dimensions must be positive.";
            return false;
        }

        if (colorRange is > 1 || colorMatrix is > 1 || transferFunction is not (null or 0 or 7) || colorPrimaries is not (null or 0 or 1))
        {
            error = "PipeWire negotiated video color metadata is not an 8-bit sRGB-compatible range.";
            return false;
        }

        if (!Enum.IsDefined((PipeWireVideoFormat)formatId))
        {
            error = $"PipeWire negotiated video format id {formatId.ToString(CultureInfo.InvariantCulture)} is unsupported.";
            return false;
        }

        if (formatId is (uint)PipeWireVideoFormat.Rgba or (uint)PipeWireVideoFormat.Bgra or (uint)PipeWireVideoFormat.Argb or (uint)PipeWireVideoFormat.Abgr)
        {
            error = "PipeWire negotiated an alpha-bearing video format that this opaque screen frame path cannot represent safely.";
            return false;
        }

        try
        {
            layout = new PipeWireVideoLayout(width, height, (PipeWireVideoFormat)formatId);
            _ = layout.MinimumBufferSize;
            return true;
        }
        catch (OverflowException)
        {
            error = "PipeWire negotiated video dimensions overflow the supported buffer size.";
            return false;
        }
    }

    private static bool TryReadProperty(
        IntPtr pod,
        int totalSize,
        ref int offset,
        out uint key,
        out uint valueType,
        out uint valueSize,
        out int valueOffset)
    {
        key = 0;
        valueType = 0;
        valueSize = 0;
        valueOffset = 0;
        if (offset < 0 || offset > totalSize - PropertyHeaderSize)
        {
            return false;
        }

        key = ReadUInt32(pod, offset);
        valueSize = ReadUInt32(pod, offset + 8);
        valueType = ReadUInt32(pod, offset + 12);
        valueOffset = checked(offset + PropertyHeaderSize);
        if (valueSize > (uint)(totalSize - valueOffset))
        {
            return false;
        }

        var alignedSize = Align8(checked((int)valueSize));
        if (alignedSize > totalSize - valueOffset)
        {
            return false;
        }

        offset = checked(valueOffset + alignedSize);
        return true;
    }

    private static bool TryReadId(IntPtr pod, uint valueType, uint valueSize, int valueOffset, out uint value)
    {
        value = 0;
        if (valueType == SpaTypeId && valueSize >= sizeof(uint))
        {
            value = ReadUInt32(pod, valueOffset);
            return true;
        }

        if (valueType == SpaTypeChoice && valueSize >= 20 && ReadUInt32(pod, valueOffset + 12) == SpaTypeId)
        {
            value = ReadUInt32(pod, valueOffset + 16);
            return value is not 0;
        }

        return false;
    }

    private static bool TryReadRectangle(IntPtr pod, uint valueType, uint valueSize, int valueOffset, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (valueType == SpaTypeRectangle && valueSize >= sizeof(uint) * 2)
        {
            return TryConvertRectangle(pod, valueOffset, out width, out height);
        }

        if (valueType == SpaTypeChoice && valueSize >= 24 && ReadUInt32(pod, valueOffset + 12) == SpaTypeRectangle)
        {
            var childSize = ReadUInt32(pod, valueOffset + 8);
            if (childSize >= sizeof(uint) * 2)
            {
                return TryConvertRectangle(pod, valueOffset + 16, out width, out height);
            }
        }

        return false;
    }

    private static bool TryConvertRectangle(IntPtr pod, int valueOffset, out int width, out int height)
    {
        var rawWidth = ReadUInt32(pod, valueOffset);
        var rawHeight = ReadUInt32(pod, valueOffset + sizeof(uint));
        if (rawWidth <= int.MaxValue && rawHeight <= int.MaxValue)
        {
            width = (int)rawWidth;
            height = (int)rawHeight;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static int Align8(int value) => checked((value + 7) & ~7);

    private static uint ReadUInt32(IntPtr address, int offset = 0) => unchecked((uint)Marshal.ReadInt32(address, offset));
}
