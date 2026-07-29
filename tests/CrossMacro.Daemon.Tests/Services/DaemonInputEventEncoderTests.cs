namespace CrossMacro.Daemon.Tests.Services;

public sealed class DaemonInputEventEncoderTests
{
    [Theory]
    [InlineData(UInputNative.REL_WHEEL_HI_RES)]
    [InlineData(UInputNative.REL_HWHEEL_HI_RES)]
    public void Write_WhenHighResolutionWheelEvent_EncodesScrollAndPreservesRawValues(ushort code)
    {
        const int value = 120;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        DaemonInputEventEncoder.Write(writer, new UInputNative.input_event
        {
            type = UInputNative.EV_REL,
            code = code,
            value = value,
        });

        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseScroll, reader.ReadByte());
        Assert.Equal((int)code, reader.ReadInt32());
        Assert.Equal(value, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);
    }

    [Theory]
    [InlineData(UInputNative.REL_X)]
    [InlineData(UInputNative.REL_Y)]
    public void Write_WhenRelativeAxisEvent_EncodesMouseMove(ushort code)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        DaemonInputEventEncoder.Write(writer, new UInputNative.input_event
        {
            type = UInputNative.EV_REL,
            code = code,
            value = 10,
        });

        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseMove, reader.ReadByte());
        Assert.Equal((int)code, reader.ReadInt32());
        Assert.Equal(10, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);
    }
}
