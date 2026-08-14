namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireFormatTests
{
    [Fact]
    public void SpaFormatPodBuilder_UsesOfficialSpaParameterIds()
    {
        var formatPod = SpaFormatPodBuilder.CreateRawVideoEnumFormat(3, 2);
        var bufferPod = SpaFormatPodBuilder.CreateCpuBufferParams(3, 2);

        try
        {
            Assert.Equal(3U, PipeWireConstants.SpaParamEnumFormat);
            Assert.Equal(4U, PipeWireConstants.SpaParamFormat);
            Assert.Equal(5U, PipeWireConstants.SpaParamBuffers);
            Assert.Equal(PipeWireConstants.SpaParamEnumFormat, ReadUInt32(formatPod, 12));
            Assert.Equal(PipeWireConstants.SpaParamBuffers, ReadUInt32(bufferPod, 12));
        }
        finally
        {
            Marshal.FreeHGlobal(formatPod);
            Marshal.FreeHGlobal(bufferPod);
        }
    }

    [Theory]
    [InlineData(8, ScreenPixelFormat.Xrgb8888)]
    [InlineData(7, ScreenPixelFormat.Xbgr8888)]
    [InlineData(10, ScreenPixelFormat.Xbgr8888)]
    [InlineData(15, ScreenPixelFormat.Rgb24)]
    public void SpaFormatPodParser_ReadsNegotiatedDimensionsAndFormat(uint format, ScreenPixelFormat expectedSourceFormat)
    {
        var pod = SpaFormatPodBuilder.CreateRawVideoEnumFormat(3, 2, format);
        try
        {
            Assert.True(SpaFormatPodParser.TryReadFormat(pod, out var layout, out var error), error);
            Assert.Equal(3, layout.Width);
            Assert.Equal(2, layout.Height);
            Assert.Equal(format, (uint)layout.Format);
            Assert.Equal(ScreenFrame.GetBytesPerPixel(expectedSourceFormat), layout.BytesPerPixel);
        }
        finally
        {
            Marshal.FreeHGlobal(pod);
        }
    }

    [Fact]
    public void SpaFormatPodParser_RejectsUnsupportedHighBitDepthFormat()
    {
        var pod = SpaFormatPodBuilder.CreateRawVideoEnumFormat(3, 2, videoFormat: 100);
        try
        {
            Assert.False(SpaFormatPodParser.TryReadFormat(pod, out _, out var error));
            Assert.Contains("unsupported", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.FreeHGlobal(pod);
        }
    }

    [Fact]
    public void SpaFormatPodParser_RejectsAlphaBearingFormatWithoutAlphaContract()
    {
        var pod = SpaFormatPodBuilder.CreateRawVideoEnumFormat(3, 2, videoFormat: (uint)PipeWireVideoFormat.Bgra);
        try
        {
            Assert.False(SpaFormatPodParser.TryReadFormat(pod, out _, out var error));
            Assert.Contains("alpha-bearing", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.FreeHGlobal(pod);
        }
    }

    [Fact]
    public void SpaFormatPodParser_ReadsChoiceRectangleFromNegotiatedFormat()
    {
        var pod = CreateChoiceSizeFormatPod(2560, 1440);

        try
        {
            Assert.True(SpaFormatPodParser.TryReadFormat(pod, out var layout, out var error), error);
            Assert.Equal(2560, layout.Width);
            Assert.Equal(1440, layout.Height);
            Assert.Equal(PipeWireVideoFormat.Bgrx, layout.Format);
        }
        finally
        {
            Marshal.FreeHGlobal(pod);
        }
    }

    [Theory]
    [MemberData(nameof(ChannelOrderCases))]
    public void PipeWireVideoLayout_ConvertsChannelOrderToCanonicalXrgb(
        uint formatId,
        byte[] source,
        byte[] expected)
    {
        var layout = new PipeWireVideoLayout(1, 1, (PipeWireVideoFormat)formatId);
        var target = new byte[4];

        layout.WriteXrgb(source, 0, target, 0);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void SpaFormatPodBuilder_AllowsThreeByteNegotiatedStride()
    {
        var pod = SpaFormatPodBuilder.CreateCpuBufferParams(3, 2, requestedStride: 9);

        try
        {
            Assert.NotEqual(IntPtr.Zero, pod);
        }
        finally
        {
            Marshal.FreeHGlobal(pod);
        }
    }

    [Fact]
    public void SpaFormatPodBuilder_AdvertisesMemFdAndRequiredMetadata()
    {
        var bufferPod = SpaFormatPodBuilder.CreateCpuBufferParams(3, 2);
        var headerPod = SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaHeader, 32);
        var damagePod = SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaVideoDamage, 64, 16, 64);

        try
        {
            Assert.Equal(PipeWireConstants.SpaTypeObjectParamMeta, ReadUInt32(headerPod, 8));
            Assert.Equal(PipeWireConstants.SpaParamMeta, ReadUInt32(headerPod, 12));
            Assert.Equal(PipeWireConstants.SpaMetaHeader, ReadUInt32(headerPod, 32));
            Assert.Equal(32U, ReadUInt32(headerPod, 56));
            Assert.Equal(PipeWireConstants.SpaMetaVideoDamage, ReadUInt32(damagePod, 32));
            Assert.Equal(19U, ReadUInt32(damagePod, 52));
            Assert.Equal(1U, ReadUInt32(damagePod, 56));
            Assert.Equal(16U, ReadUInt32(damagePod, 76));
            Assert.Equal(64U, ReadUInt32(damagePod, 80));

            var buffers = FindProperty(bufferPod, key: 1U);
            Assert.Equal(19U, buffers.ValueType);
            Assert.Equal(28U, buffers.ValueSize);
            Assert.Equal(1U, ReadUInt32(bufferPod, buffers.ValueOffset));
            Assert.Equal(4U, ReadUInt32(bufferPod, buffers.ValueOffset + 8));
            Assert.Equal(4U, ReadUInt32(bufferPod, buffers.ValueOffset + 12));
            Assert.Equal(3U, ReadUInt32(bufferPod, buffers.ValueOffset + 16));
            Assert.Equal(2U, ReadUInt32(bufferPod, buffers.ValueOffset + 20));
            Assert.Equal(4U, ReadUInt32(bufferPod, buffers.ValueOffset + 24));

            var dataType = FindProperty(bufferPod, key: 6U);
            Assert.Equal(19U, dataType.ValueType);
            Assert.Equal(20U, dataType.ValueSize);
            Assert.Equal(4U, ReadUInt32(bufferPod, dataType.ValueOffset));
        }
        finally
        {
            Marshal.FreeHGlobal(bufferPod);
            Marshal.FreeHGlobal(headerPod);
            Marshal.FreeHGlobal(damagePod);
        }
    }

    [Fact]
    public void SpaFormatPodBuilder_RejectsIncompleteMetadataRange()
    {
        Assert.Throws<ArgumentException>(() => SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaVideoDamage, 16, minimumSize: 16));
    }

    [Fact]
    public void PipeWireFrameRowConverter_UsesOnlyNegotiatedPixelBytes()
    {
        var layout = new PipeWireVideoLayout(3, 1, PipeWireVideoFormat.Bgrx);
        var source = new byte[]
        {
            0x33, 0x22, 0x11, 0x00,
            0x66, 0x55, 0x44, 0x00,
            0xCC, 0xBB, 0xAA, 0x00,
        };
        var target = new byte[8];

        PipeWireFrameRowConverter.Convert(source, layout, sourceLogicalWidth: 2, sourceStartX: 0, target);

        Assert.Equal(new byte[] { 0x33, 0x22, 0x11, 0xFF, 0xCC, 0xBB, 0xAA, 0xFF }, target);
    }

    [Fact]
    public void PipeWireFrameSequence_RejectsCallbackThatBeganBeforeRequest()
    {
        var sequence = new PipeWireFrameSequence();
        var oldCallback = sequence.BeginProcess();
        var request = sequence.Snapshot();

        Assert.False(PipeWireFrameSequence.IsNewerThan(oldCallback, request));

        var newCallback = sequence.BeginProcess();

        Assert.True(PipeWireFrameSequence.IsNewerThan(newCallback, request));
    }

    public static TheoryData<uint, byte[], byte[]> ChannelOrderCases => new()
    {
        { (uint)PipeWireVideoFormat.Rgbx, [0x11, 0x22, 0x33, 0x00], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Bgrx, [0x33, 0x22, 0x11, 0x00], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Xrgb, [0x00, 0x11, 0x22, 0x33], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Xbgr, [0x00, 0x33, 0x22, 0x11], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Rgba, [0x11, 0x22, 0x33, 0x44], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Bgra, [0x33, 0x22, 0x11, 0x44], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Argb, [0x44, 0x11, 0x22, 0x33], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Abgr, [0x44, 0x33, 0x22, 0x11], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Rgb, [0x11, 0x22, 0x33], [0x33, 0x22, 0x11, 0xFF] },
        { (uint)PipeWireVideoFormat.Bgr, [0x33, 0x22, 0x11], [0x33, 0x22, 0x11, 0xFF] },
    };

    private static uint ReadUInt32(IntPtr address, int offset) => unchecked((uint)Marshal.ReadInt32(address, offset));

    private static (uint ValueType, uint ValueSize, int ValueOffset) FindProperty(IntPtr pod, uint key)
    {
        var totalSize = checked((int)ReadUInt32(pod, 0) + 8);
        for (var offset = 16; offset <= totalSize - 16;)
        {
            var propertyKey = ReadUInt32(pod, offset);
            var valueSize = ReadUInt32(pod, offset + 8);
            var valueType = ReadUInt32(pod, offset + 12);
            var valueOffset = offset + 16;
            if (propertyKey == key)
            {
                return (valueType, valueSize, valueOffset);
            }

            offset = checked(valueOffset + ((int)valueSize + 7 & ~7));
        }

        throw new Xunit.Sdk.XunitException($"SPA property {key} was not found.");
    }

    private static IntPtr CreateChoiceSizeFormatPod(uint width, uint height)
    {
        using var stream = new MemoryStream(256);
        using var writer = new BinaryWriter(stream);
        writer.Write(0u);
        writer.Write(15u);
        writer.Write(0x40003u);
        writer.Write(PipeWireConstants.SpaParamFormat);
        WriteIdProperty(writer, 1u, 2u);
        WriteIdProperty(writer, 2u, 1u);
        WriteIdProperty(writer, 0x20001u, (uint)PipeWireVideoFormat.Bgrx);
        WritePropertyHeader(writer, 0x20003u, 40u, 19u);
        writer.Write(1u);
        writer.Write(0u);
        writer.Write(8u);
        writer.Write(10u);
        WriteRectangle(writer, width, height);
        WriteRectangle(writer, 1u, 1u);
        WriteRectangle(writer, 8192u, 4320u);
        var data = stream.ToArray();
        BitConverter.GetBytes((uint)(data.Length - 8)).CopyTo(data, 0);
        var memory = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, memory, data.Length);
        return memory;
    }

    private static void WriteIdProperty(BinaryWriter writer, uint key, uint value)
    {
        WritePropertyHeader(writer, key, 4u, 3u);
        writer.Write(value);
        Align(writer);
    }

    private static void WritePropertyHeader(BinaryWriter writer, uint key, uint valueSize, uint valueType)
    {
        writer.Write(key);
        writer.Write(0u);
        writer.Write(valueSize);
        writer.Write(valueType);
    }

    private static void WriteRectangle(BinaryWriter writer, uint width, uint height)
    {
        writer.Write(width);
        writer.Write(height);
    }

    private static void Align(BinaryWriter writer)
    {
        while ((writer.BaseStream.Position & 7) is not 0)
        {
            writer.Write((byte)0);
        }
    }
}
