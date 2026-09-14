namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public sealed class UInputOwnershipTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Handle_DisposeClosesOnceAndDestroysOnlyCreatedDevice(bool created, int expectedDestroyCount)
    {
        var destroyed = new List<int>();
        var closed = new List<int>();
        var handle = new UInputDeviceHandle(0, destroyed.Add, closed.Add);
        if (created)
        {
            handle.MarkCreated();
        }

        handle.Dispose();
        handle.Dispose();

        Assert.Equal(expectedDestroyCount, destroyed.Count);
        Assert.Equal(0, Assert.Single(closed));
        Assert.Equal(-1, handle.Descriptor);
    }

    [Fact]
    public void Readiness_WhenSysfsCannotBeInspected_ContinuesWithoutWaiting()
    {
        var waits = 0;
        var readiness = new UInputDeviceReadiness(() => "input1", () => true,
            _ => (null, false), synchronousDelay: _ => waits++);

        readiness.Wait();

        Assert.Equal(0, waits);
    }

    [Fact]
    public void Readiness_WhenNodeNeverAppears_StopsAtItsBudget()
    {
        var time = new ManualTime();
        var readiness = new UInputDeviceReadiness(() => "input1", () => true,
            _ => (null, true), time, time.Advance);

        readiness.Wait();

        Assert.InRange(time.GetTimestamp(), 500, 505);
    }

    [Fact]
    public async Task Readiness_WhenCanceled_DoesNotInspectNativeDevice()
    {
        var inspected = false;
        var readiness = new UInputDeviceReadiness(() => { inspected = true; return "input1"; },
            () => true, _ => (null, true));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.WaitAsync(cancellation.Token));
        Assert.False(inspected);
    }

    [Fact]
    public void Handle_DestroyFailureStillClosesTheDescriptorExactlyOnce()
    {
        var closed = 0;
        var handle = new UInputDeviceHandle(5, _ => throw new IOException("destroy failed"), _ => closed++);
        handle.MarkCreated();
        handle.Dispose();
        handle.Dispose();
        Assert.Equal(1, closed);
    }

    [Fact]
    public void PacketExecutor_RelativeEventsPreserveOrderAndSingleReport()
    {
        var writer = new EventWriter();
        var packets = new UInputPacketExecutor(0, 0);
        packets.Send(UInputNative.EV_REL, UInputNative.REL_X, 25, writer);
        packets.Send(UInputNative.EV_REL, UInputNative.REL_Y, -3, writer);
        packets.Send(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0, writer);
        Assert.Equal(new (ushort, ushort, int)[] { (2, 0, 25), (2, 1, -3), (0, 0, 0) }, writer.Events);
    }

    private sealed class EventWriter : IUInputEventWriter
    {
        public List<(ushort Type, ushort Code, int Value)> Events { get; } = [];
        public void WriteEvent(ushort type, ushort code, int value) => Events.Add((type, code, value));
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _milliseconds;
        internal void Advance(TimeSpan duration) => _milliseconds += (long)duration.TotalMilliseconds;
    }
}
