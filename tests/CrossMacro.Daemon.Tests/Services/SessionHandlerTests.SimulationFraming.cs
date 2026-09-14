namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{
    [LinuxTheory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(5, true)]
    public async Task RunAsync_WhenBatchValidationFailsBeforeLastEvent_ConsumesFrameWithoutDispatchingItsRemainingBytes(
        int invalidEventIndex,
        bool exceedsTotalDelay)
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var capture = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, capture);
        await using var sockets = await UnixSocketPair.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5), TimeProvider.System);
        var run = StartSessionOnBackgroundThreadAsync(handler, sockets.Server, uid: 1001, pid: 4321, cancellation.Token);
        using var stream = new NetworkStream(sockets.Client, ownsSocket: false);
        stream.ReadTimeout = 2000;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);
        CompleteHandshake(reader, writer);
        SendStartCaptureCommand(reader, writer, requestId: 8000);

        var eventCount = invalidEventIndex + 2;
        IpcMessageCodec.WriteSimulationBatchHeader(writer, requestId: 8001, eventCount);
        for (var index = 0; index < eventCount; index++)
        {
            var delay = exceedsTotalDelay ? IpcProtocol.MaxSimulationBatchDelayMicroseconds : 0;
            if (!exceedsTotalDelay && index == invalidEventIndex)
            {
                delay = -1;
            }

            IpcMessageCodec.WriteSimulationEvent(writer, new IpcSimulationRequest
            {
                // Its low byte is also StopCapture's opcode; it must remain payload.
                Type = UInputNative.EV_ABS,
                Code = UInputNative.ABS_X,
                Value = 123,
                DelayAfterMicroseconds = delay,
            });
        }

        writer.Flush();
        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(8001, reader.ReadInt32());
        Assert.Contains("delay", reader.ReadString(), StringComparison.Ordinal);
        SendStartCaptureCommand(reader, writer, requestId: 8002);

        Assert.Equal(2, capture.StartCaptureCalls);
        Assert.Equal(0, security.CaptureStopCalls);
        Assert.Empty(virtualDevice.SentEvents);
        sockets.Client.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cancellation.Token);
    }

    [LinuxTheory]
    [InlineData(-1)]
    [InlineData(IpcProtocol.MaxSimulationBatchEvents + 1)]
    public async Task RunAsync_WhenBatchCountHasNoTrustedFrameBoundary_RejectsAndClosesWithoutReadingAnotherCommand(int eventCount)
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var capture = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, capture);
        await using var sockets = await UnixSocketPair.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5), TimeProvider.System);
        var run = StartSessionOnBackgroundThreadAsync(handler, sockets.Server, uid: 1001, pid: 4321, cancellation.Token);
        using var stream = new NetworkStream(sockets.Client, ownsSocket: false);
        stream.ReadTimeout = 2000;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);
        CompleteHandshake(reader, writer);
        IpcMessageCodec.WriteSimulationBatchHeader(writer, requestId: 8003, eventCount);
        writer.Write((byte)IpcOpCode.StopCapture);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(8003, reader.ReadInt32());
        Assert.Contains("event count", reader.ReadString(), StringComparison.Ordinal);
        await run.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cancellation.Token);

        Assert.Equal(0, security.CaptureStopCalls);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.True(capture.StopCaptureCalls > 0);
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(1));
    }

    [LinuxFact]
    public async Task RunAsync_WhenBatchEndsAfterAnInvalidEvent_ClosesWithoutExecutingPartialBatch()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var capture = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, capture);
        await using var sockets = await UnixSocketPair.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5), TimeProvider.System);
        var run = StartSessionOnBackgroundThreadAsync(handler, sockets.Server, uid: 1001, pid: 4321, cancellation.Token);
        using var stream = new NetworkStream(sockets.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);
        CompleteHandshake(reader, writer);
        IpcMessageCodec.WriteSimulationBatchHeader(writer, requestId: 8004, eventCount: 2);
        IpcMessageCodec.WriteSimulationEvent(writer, new IpcSimulationRequest
        {
            Type = UInputNative.EV_ABS,
            DelayAfterMicroseconds = -1,
        });
        writer.Flush();
        sockets.Client.Shutdown(SocketShutdown.Send);

        await run.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cancellation.Token);

        Assert.Empty(virtualDevice.SentEvents);
        Assert.Empty(security.SimulationCalls);
        Assert.True(capture.StopCaptureCalls > 0);
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(1));
    }
}
