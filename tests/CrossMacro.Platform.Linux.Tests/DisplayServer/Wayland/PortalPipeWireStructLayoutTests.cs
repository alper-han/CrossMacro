
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireStructLayoutTests
{
    [Fact]
    public void PipeWireStructs_HaveExpectedX64Layouts()
    {
        AssertLayout<SpaList>(16, (nameof(SpaList.Next), 0), (nameof(SpaList.Previous), 8));
        AssertLayout<SpaCallbacks>(16, (nameof(SpaCallbacks.Functions), 0), (nameof(SpaCallbacks.Data), 8));
        AssertLayout<SpaHook>(
            48,
            (nameof(SpaHook.Link), 0),
            (nameof(SpaHook.Callbacks), 16),
            (nameof(SpaHook.Removed), 32),
            (nameof(SpaHook.Private), 40));
        AssertLayout<PipeWireStreamEvents>(
            96,
            (nameof(PipeWireStreamEvents.Version), 0),
            (nameof(PipeWireStreamEvents.Destroy), 8),
            (nameof(PipeWireStreamEvents.TriggerDone), 88));
        AssertLayout<PipeWireBuffer>(
            40,
            (nameof(PipeWireBuffer.Buffer), 0),
            (nameof(PipeWireBuffer.UserData), 8),
            (nameof(PipeWireBuffer.Size), 16),
            (nameof(PipeWireBuffer.Requested), 24),
            (nameof(PipeWireBuffer.Time), 32));
        AssertLayout<SpaBuffer>(
            24,
            (nameof(SpaBuffer.MetaCount), 0),
            (nameof(SpaBuffer.DataCount), 4),
            (nameof(SpaBuffer.Metas), 8),
            (nameof(SpaBuffer.Datas), 16));
        AssertLayout<SpaData>(
            40,
            (nameof(SpaData.Type), 0),
            (nameof(SpaData.Flags), 4),
            (nameof(SpaData.Fd), 8),
            (nameof(SpaData.MapOffset), 16),
            (nameof(SpaData.MaxSize), 20),
            (nameof(SpaData.Data), 24),
            (nameof(SpaData.Chunk), 32));
        AssertLayout<SpaChunk>(
            16,
            (nameof(SpaChunk.Offset), 0),
            (nameof(SpaChunk.Size), 4),
            (nameof(SpaChunk.Stride), 8),
            (nameof(SpaChunk.Flags), 12));
        AssertLayout<SpaMeta>(
            16,
            (nameof(SpaMeta.Type), 0),
            (nameof(SpaMeta.Size), 4),
            (nameof(SpaMeta.Data), 8));
    }

    [Fact]
    public void SpaChunk_RoundTripsThroughUnmanagedMemory()
    {
        var value = new SpaChunk { Offset = 7, Size = 4096, Stride = 128, Flags = 3 };

        var roundTrip = RoundTrip(value);

        Assert.Equal(value.Offset, roundTrip.Offset);
        Assert.Equal(value.Size, roundTrip.Size);
        Assert.Equal(value.Stride, roundTrip.Stride);
        Assert.Equal(value.Flags, roundTrip.Flags);
    }

    [Fact]
    public void SpaData_RoundTripsThroughUnmanagedMemory()
    {
        var value = new SpaData
        {
            Type = 2,
            Flags = 0xB,
            Fd = 17,
            MapOffset = 24,
            MaxSize = 4096,
            Data = new IntPtr(0x1234),
            Chunk = new IntPtr(0x5678),
        };

        var roundTrip = RoundTrip(value);

        Assert.Equal(value.Type, roundTrip.Type);
        Assert.Equal(value.Flags, roundTrip.Flags);
        Assert.Equal(value.Fd, roundTrip.Fd);
        Assert.Equal(value.MapOffset, roundTrip.MapOffset);
        Assert.Equal(value.MaxSize, roundTrip.MaxSize);
        Assert.Equal(value.Data, roundTrip.Data);
        Assert.Equal(value.Chunk, roundTrip.Chunk);
    }

    [Fact]
    public void PipeWireBuffer_RoundTripsThroughUnmanagedMemory()
    {
        var value = new PipeWireBuffer
        {
            Buffer = new IntPtr(0x1234),
            UserData = new IntPtr(0x5678),
            Size = 4096,
            Requested = 2048,
            Time = 123456,
        };

        var roundTrip = RoundTrip(value);

        Assert.Equal(value.Buffer, roundTrip.Buffer);
        Assert.Equal(value.UserData, roundTrip.UserData);
        Assert.Equal(value.Size, roundTrip.Size);
        Assert.Equal(value.Requested, roundTrip.Requested);
        Assert.Equal(value.Time, roundTrip.Time);
    }

    [Fact]
    public void PipeWireStreamEvents_RoundTripsThroughUnmanagedMemory()
    {
        var value = new PipeWireStreamEvents
        {
            Version = 2,
            Destroy = new IntPtr(0x1000),
            StateChanged = new IntPtr(0x2000),
            ControlInfo = new IntPtr(0x3000),
            IoChanged = new IntPtr(0x4000),
            ParamChanged = new IntPtr(0x5000),
            AddBuffer = new IntPtr(0x6000),
            RemoveBuffer = new IntPtr(0x7000),
            Process = new IntPtr(0x8000),
            Drained = new IntPtr(0x9000),
            Command = new IntPtr(0xA000),
            TriggerDone = new IntPtr(0xB000),
        };

        var roundTrip = RoundTrip(value);

        Assert.Equal(value.Version, roundTrip.Version);
        Assert.Equal(value.Destroy, roundTrip.Destroy);
        Assert.Equal(value.StateChanged, roundTrip.StateChanged);
        Assert.Equal(value.ControlInfo, roundTrip.ControlInfo);
        Assert.Equal(value.IoChanged, roundTrip.IoChanged);
        Assert.Equal(value.ParamChanged, roundTrip.ParamChanged);
        Assert.Equal(value.AddBuffer, roundTrip.AddBuffer);
        Assert.Equal(value.RemoveBuffer, roundTrip.RemoveBuffer);
        Assert.Equal(value.Process, roundTrip.Process);
        Assert.Equal(value.Drained, roundTrip.Drained);
        Assert.Equal(value.Command, roundTrip.Command);
        Assert.Equal(value.TriggerDone, roundTrip.TriggerDone);
    }

    private static void AssertLayout<T>(int size, params (string Field, int Offset)[] fields)
        where T : struct
    {
        Assert.Equal(size, Marshal.SizeOf<T>());

        foreach (var (field, offset) in fields)
        {
            Assert.Equal(offset, Marshal.OffsetOf<T>(field).ToInt32());
        }
    }

    private static T RoundTrip<T>(T value)
        where T : unmanaged
    {
        Span<T> storage = stackalloc T[1];
        Span<byte> bytes = MemoryMarshal.AsBytes(storage);
        MemoryMarshal.Write(bytes, in value);
        return MemoryMarshal.Read<T>(bytes);
    }
}
