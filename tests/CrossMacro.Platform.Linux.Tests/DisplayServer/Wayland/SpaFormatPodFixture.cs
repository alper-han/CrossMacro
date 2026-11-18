namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

/// <summary>
/// Builds a negotiated raw-video format pod, as a PipeWire server would send it, for parser tests.
/// The layout follows <c>spa_pod_object</c> with one fixed value per property.
/// </summary>
internal static class SpaFormatPodFixture
{
    private const uint SpaTypeId = 3;
    private const uint SpaTypeRectangle = 10;
    private const uint SpaTypeObject = 15;
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

    /// <summary>Returns native memory the caller releases with <see cref="Marshal.FreeHGlobal(IntPtr)"/>.</summary>
    public static IntPtr CreateRawVideoFormat(
        int width,
        int height,
        uint videoFormat,
        uint? colorRange = null,
        uint? colorMatrix = null,
        uint? transferFunction = null,
        uint? colorPrimaries = null)
    {
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(SpaTypeObject);
        writer.Write(SpaTypeObjectFormat);
        writer.Write(PipeWireConstants.SpaParamEnumFormat);
        WriteId(writer, SpaFormatMediaType, SpaMediaTypeVideo);
        WriteId(writer, SpaFormatMediaSubtype, SpaMediaSubtypeRaw);
        WriteId(writer, SpaFormatVideoFormat, videoFormat);
        WriteRectangle(writer, SpaFormatVideoSize, (uint)width, (uint)height);
        WriteOptionalId(writer, SpaFormatVideoColorRange, colorRange);
        WriteOptionalId(writer, SpaFormatVideoColorMatrix, colorMatrix);
        WriteOptionalId(writer, SpaFormatVideoTransferFunction, transferFunction);
        WriteOptionalId(writer, SpaFormatVideoColorPrimaries, colorPrimaries);

        var data = stream.ToArray();
        BitConverter.GetBytes((uint)(data.Length - 8)).CopyTo(data, 0);
        var memory = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, memory, data.Length);
        return memory;
    }

    private static void WriteId(BinaryWriter writer, uint key, uint value)
    {
        WritePropertyHeader(writer, key, 4, SpaTypeId);
        writer.Write(value);
        Align(writer);
    }

    private static void WriteOptionalId(BinaryWriter writer, uint key, uint? value)
    {
        if (value is { } id)
        {
            WriteId(writer, key, id);
        }
    }

    private static void WriteRectangle(BinaryWriter writer, uint key, uint width, uint height)
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
