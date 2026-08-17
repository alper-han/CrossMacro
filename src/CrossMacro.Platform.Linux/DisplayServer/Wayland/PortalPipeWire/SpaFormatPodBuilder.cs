
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class SpaFormatPodBuilder
{
    private const uint SpaTypeId = 3;
    private const uint SpaTypeInt = 4;
    private const uint SpaTypeObject = 15;
    private const uint SpaTypeChoice = 19;
    private const uint SpaTypeRectangle = 10;
    private const uint SpaTypeObjectFormat = 0x40003;
    private const uint SpaTypeObjectParamBuffers = 0x40004;
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
    private static readonly uint[] SupportedScreenFormats =
    [
        (uint)PipeWireVideoFormat.Rgbx,
        (uint)PipeWireVideoFormat.Bgra,
        (uint)PipeWireVideoFormat.Rgba,
        (uint)PipeWireVideoFormat.Bgrx,
        (uint)PipeWireVideoFormat.Xrgb,
        (uint)PipeWireVideoFormat.Xbgr,
        (uint)PipeWireVideoFormat.Argb,
        (uint)PipeWireVideoFormat.Abgr,
        (uint)PipeWireVideoFormat.Rgb,
        (uint)PipeWireVideoFormat.Bgr,
    ];
    private const uint SpaParamBuffersBuffers = 1;
    private const uint SpaParamBuffersBlocks = 2;
    private const uint SpaParamBuffersSize = 3;
    private const uint SpaParamBuffersStride = 4;
    private const uint SpaParamBuffersAlign = 5;
    private const uint SpaParamBuffersDataType = 6;

    public static IntPtr CreateRawVideoEnumFormat(int width, int height)
        => CreateRawVideoEnumFormatChoices(width, height, SupportedScreenFormats);

    internal static IntPtr CreateRawVideoEnumFormatChoices(
        int width,
        int height,
        IReadOnlyList<uint> videoFormats)
    {
        ArgumentNullException.ThrowIfNull(videoFormats);
        if (videoFormats.Count is 0)
        {
            throw new ArgumentException("At least one PipeWire video format is required.", nameof(videoFormats));
        }

        ValidateDimensions(width, height);
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectFormat);
        writer.Write(PipeWireConstants.SpaParamEnumFormat);
        WriteIdProperty(writer, SpaFormatMediaType, SpaMediaTypeVideo);
        WriteIdProperty(writer, SpaFormatMediaSubtype, SpaMediaSubtypeRaw);
        WriteChoiceEnumIdProperty(writer, SpaFormatVideoFormat, videoFormats);
        WriteRectangleProperty(writer, SpaFormatVideoSize, (uint)width, (uint)height);
        return CopyToNative(stream);
    }

    internal static IntPtr CreateRawVideoEnumFormat(
        int width,
        int height,
        uint videoFormat,
        uint? colorRange = null,
        uint? colorMatrix = null,
        uint? transferFunction = null,
        uint? colorPrimaries = null)
    {
        ValidateDimensions(width, height);
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectFormat);
        writer.Write(PipeWireConstants.SpaParamEnumFormat);
        WriteIdProperty(writer, SpaFormatMediaType, SpaMediaTypeVideo);
        WriteIdProperty(writer, SpaFormatMediaSubtype, SpaMediaSubtypeRaw);
        WriteIdProperty(writer, SpaFormatVideoFormat, videoFormat);
        WriteRectangleProperty(writer, SpaFormatVideoSize, (uint)width, (uint)height);
        WriteOptionalIdProperty(writer, SpaFormatVideoColorRange, colorRange);
        WriteOptionalIdProperty(writer, SpaFormatVideoColorMatrix, colorMatrix);
        WriteOptionalIdProperty(writer, SpaFormatVideoTransferFunction, transferFunction);
        WriteOptionalIdProperty(writer, SpaFormatVideoColorPrimaries, colorPrimaries);
        return CopyToNative(stream);
    }

    public static IntPtr CreateCpuBufferParams(int width, int height, int? requestedStride = null)
    {
        ValidateDimensions(width, height);
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        var minimumStride = requestedStride ?? checked(width * PipeWireConstants.Xrgb8888BytesPerPixel);
        var stride = requestedStride ?? minimumStride;
        if (minimumStride <= 0 || stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedStride), requestedStride, "PipeWire buffer stride is smaller than the negotiated row width.");
        }

        var size = checked(stride * height);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectParamBuffers);
        writer.Write(PipeWireConstants.SpaParamBuffers);
        WriteChoiceRangeIntProperty(writer, SpaParamBuffersBuffers, defaultValue: 3, minimum: 2, maximum: 4);
        WriteIntProperty(writer, SpaParamBuffersBlocks, 1);
        WriteIntProperty(writer, SpaParamBuffersSize, size);
        WriteIntProperty(writer, SpaParamBuffersStride, stride);
        WriteIntProperty(writer, SpaParamBuffersAlign, 16);
        WriteChoiceFlagsIntProperty(writer, SpaParamBuffersDataType, 1 << (int)PipeWireBufferTypePolicy.SpaDataMemFd);
        return CopyToNative(stream);
    }

    internal static IntPtr CreateMetaParameter(uint metaType, int size, int? minimumSize = null, int? maximumSize = null)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "PipeWire metadata size must be positive.");
        }

        if ((minimumSize is null) != (maximumSize is null))
        {
            throw new ArgumentException("PipeWire metadata size range must provide both minimum and maximum values.", nameof(minimumSize));
        }

        if (minimumSize is not null && (minimumSize <= 0 || maximumSize < minimumSize || size < minimumSize || size > maximumSize))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSize), minimumSize, "PipeWire metadata size range is invalid.");
        }

        using var stream = new MemoryStream(128);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(PipeWireConstants.SpaTypeObjectParamMeta);
        writer.Write(PipeWireConstants.SpaParamMeta);
        WriteIdProperty(writer, PipeWireConstants.SpaParamMetaType, metaType);
        if (minimumSize is { } min && maximumSize is { } max)
        {
            WriteChoiceRangeIntProperty(writer, PipeWireConstants.SpaParamMetaSize, size, min, max);
        }
        else
        {
            WriteIntProperty(writer, PipeWireConstants.SpaParamMetaSize, size);
        }

        return CopyToNative(stream);
    }

    private static IntPtr CopyToNative(MemoryStream stream)
    {
        var data = stream.ToArray();
        BitConverter.GetBytes((uint)(data.Length - 8)).CopyTo(data, 0);
        var memory = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, memory, data.Length);
        return memory;
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "PipeWire video width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "PipeWire video height must be positive.");
        }
    }

    private static void WriteIdProperty(BinaryWriter writer, uint key, uint value)
    {
        WritePropertyHeader(writer, key, 4, SpaTypeId);
        writer.Write(value);
        Align(writer);
    }

    private static void WriteChoiceEnumIdProperty(BinaryWriter writer, uint key, IReadOnlyList<uint> values)
    {
        var valueSize = checked((uint)(16 + (values.Count * sizeof(uint))));
        WritePropertyHeader(writer, key, valueSize, SpaTypeChoice);
        writer.Write(3u);
        writer.Write(0u);
        writer.Write(4u);
        writer.Write(SpaTypeId);
        foreach (var value in values)
        {
            writer.Write(value);
        }

        Align(writer);
    }

    private static void WriteOptionalIdProperty(BinaryWriter writer, uint key, uint? value)
    {
        if (value is { } id)
        {
            WriteIdProperty(writer, key, id);
        }
    }

    private static void WriteIntProperty(BinaryWriter writer, uint key, int value)
    {
        WritePropertyHeader(writer, key, 4, SpaTypeInt);
        writer.Write(value);
        Align(writer);
    }

    private static void WriteChoiceFlagsIntProperty(BinaryWriter writer, uint key, int flags)
    {
        WritePropertyHeader(writer, key, 20, SpaTypeChoice);
        writer.Write(4u);
        writer.Write(0u);
        writer.Write(4u);
        writer.Write(SpaTypeInt);
        writer.Write(flags);
        Align(writer);
    }

    private static void WriteChoiceRangeIntProperty(BinaryWriter writer, uint key, int defaultValue, int minimum, int maximum)
    {
        WritePropertyHeader(writer, key, 28, SpaTypeChoice);
        writer.Write(1u);
        writer.Write(0u);
        writer.Write(4u);
        writer.Write(SpaTypeInt);
        writer.Write(defaultValue);
        writer.Write(minimum);
        writer.Write(maximum);
        Align(writer);
    }

    private static void WriteRectangleProperty(BinaryWriter writer, uint key, uint width, uint height)
    {
        WritePropertyHeader(writer, key, 8, SpaTypeRectangle);
        writer.Write(width);
        writer.Write(height);
        Align(writer);
    }

    private static void WritePropertyHeader(BinaryWriter writer, uint key, uint valueSize, uint valueType)
    {
        writer.Write(key);
        writer.Write(0u);
        writer.Write(valueSize);
        writer.Write(valueType);
    }

    private static void Align(BinaryWriter writer)
    {
        while ((writer.BaseStream.Position & 7) != 0)
        {
            writer.Write((byte)0);
        }
    }
}
