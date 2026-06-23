using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class SpaFormatPodBuilder
{
    private const uint SpaTypeId = 3;
    private const uint SpaTypeInt = 4;
    private const uint SpaTypeObject = 15;
    private const uint SpaTypeChoice = 19;
    private const uint SpaTypeRectangle = 10;
    private const uint SpaTypeFraction = 11;
    private const uint SpaTypeObjectFormat = 0x40003;
    private const uint SpaTypeObjectParamBuffers = 0x40004;
    private const uint SpaParamEnumFormat = 2;
    private const uint SpaParamBuffers = 4;
    private const uint SpaMediaTypeVideo = 2;
    private const uint SpaMediaSubtypeRaw = 1;
    private const uint SpaFormatMediaType = 1;
    private const uint SpaFormatMediaSubtype = 2;
    private const uint SpaFormatVideoFormat = 0x20001;
    private const uint SpaFormatVideoSize = 0x20003;
    private const uint SpaFormatVideoFramerate = 0x20004;
    private const uint SpaVideoFormatBgrx = 8;
    private const uint SpaParamBuffersBuffers = 1;
    private const uint SpaParamBuffersBlocks = 2;
    private const uint SpaParamBuffersSize = 3;
    private const uint SpaParamBuffersStride = 4;
    private const uint SpaParamBuffersAlign = 5;
    private const uint SpaParamBuffersDataType = 6;
    private const uint SpaDataMemPtr = 1;
    private const uint SpaDataMemFd = 2;

    public static IntPtr CreateRawVideoEnumFormat(int width, int height)
    {
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectFormat);
        writer.Write(SpaParamEnumFormat);
        WriteIdProperty(writer, SpaFormatMediaType, SpaMediaTypeVideo);
        WriteIdProperty(writer, SpaFormatMediaSubtype, SpaMediaSubtypeRaw);
        WriteIdProperty(writer, SpaFormatVideoFormat, SpaVideoFormatBgrx);
        WriteRectangleProperty(writer, SpaFormatVideoSize, (uint)width, (uint)height);
        WriteFractionProperty(writer, SpaFormatVideoFramerate, 0, 1);
        return CopyToNative(stream);
    }

    public static IntPtr CreateCpuBufferParams(int width, int height)
    {
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        var stride = checked(width * PipeWireConstants.Xrgb8888BytesPerPixel);
        var size = checked(stride * height);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectParamBuffers);
        writer.Write(SpaParamBuffers);
        WriteIntProperty(writer, SpaParamBuffersBuffers, 4);
        WriteIntProperty(writer, SpaParamBuffersBlocks, 1);
        WriteIntProperty(writer, SpaParamBuffersSize, size);
        WriteIntProperty(writer, SpaParamBuffersStride, stride);
        WriteIntProperty(writer, SpaParamBuffersAlign, 16);
        WriteChoiceFlagsIntProperty(writer, SpaParamBuffersDataType, (int)((1u << (int)SpaDataMemPtr) | (1u << (int)SpaDataMemFd)));
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

    private static void WriteIdProperty(BinaryWriter writer, uint key, uint value)
    {
        WritePropertyHeader(writer, key, 4, SpaTypeId);
        writer.Write(value);
        Align(writer);
    }

    private static void WriteIntProperty(BinaryWriter writer, uint key, int value)
    {
        WritePropertyHeader(writer, key, 4, SpaTypeInt);
        writer.Write(value);
        Align(writer);
    }

    private static void WriteChoiceFlagsIntProperty(BinaryWriter writer, uint key, int flags)
    {
        WritePropertyHeader(writer, key, 24, SpaTypeChoice);
        writer.Write(4u);
        writer.Write(0u);
        writer.Write(4u);
        writer.Write(SpaTypeInt);
        writer.Write(flags);
        Align(writer);
    }

    private static void WriteRectangleProperty(BinaryWriter writer, uint key, uint width, uint height)
    {
        WritePropertyHeader(writer, key, 8, SpaTypeRectangle);
        writer.Write(width);
        writer.Write(height);
        Align(writer);
    }

    private static void WriteFractionProperty(BinaryWriter writer, uint key, uint numerator, uint denominator)
    {
        WritePropertyHeader(writer, key, 8, SpaTypeFraction);
        writer.Write(numerator);
        writer.Write(denominator);
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
