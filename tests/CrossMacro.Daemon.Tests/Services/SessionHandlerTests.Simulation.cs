// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{
    [LinuxFact]
    public async Task RunAsync_WhenSimulationBatchIsValid_ShouldDispatchEventsAndAcknowledge()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1001, pid: 4321, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(3030);
        writer.Write(2);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
        writer.Write((ushort)InputEventCode.KEY_A);
        writer.Write(1);
        writer.Write(0L);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT);
        writer.Write(0);
        writer.Write(0L);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchCompleted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(3030, reader.ReadInt32());
        Assert.Equal(2, reader.ReadInt32());

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            [(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, (ushort)InputEventCode.KEY_A, 1), (CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN, CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT, 0)],
            virtualDevice.SentEvents);
    }

    [LinuxFact]
    public async Task RunAsync_WhenSimulationBatchCountIsInvalid_ShouldReturnFailureAndKeepSessionAlive()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1001, pid: 4321, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(4040);
        writer.Write(0);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(4040, reader.ReadInt32());
        Assert.Contains("event count", reader.ReadString(), StringComparison.Ordinal);

        SendStartCaptureCommand(reader, writer, requestId: 4041);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.SentEvents);
    }

    [LinuxFact]
    public async Task RunAsync_WhenSimulationBatchDelayIsInvalid_ShouldReturnFailure()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1001, pid: 4321, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(5050);
        writer.Write(1);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
        writer.Write((ushort)InputEventCode.KEY_A);
        writer.Write(1);
        writer.Write(-1L);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(5050, reader.ReadInt32());
        Assert.Contains("delay", reader.ReadString(), StringComparison.Ordinal);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.SentEvents);
    }

    [LinuxFact]
    public async Task RunAsync_WhenUInputWriteFailsInSimulationBatch_ReturnsFailureAndKeepsSessionAlive()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager
        {
            ThrowOnSendEvents = new IOException("uinput event write failed: Errno=5."),
        };
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1002, pid: 4322, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(5051);
        writer.Write(1);
        writer.Write(UInputNative.EV_ABS);
        writer.Write(UInputNative.ABS_X);
        writer.Write(777);
        writer.Write(0L);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(5051, reader.ReadInt32());
        Assert.Contains("uinput event write failed", reader.ReadString(), StringComparison.Ordinal);

        SendStartCaptureCommand(reader, writer, requestId: 5052);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(security.SimulationCalls);
        _ = Assert.Single(virtualDevice.SentEvents);
    }

    [LinuxFact]
    public async Task RunAsync_WhenSimulationBatchTotalDelayIsInvalid_ShouldReturnFailure()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1001, pid: 4321, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.SimulateEventBatch);
        writer.Write(6060);
        writer.Write(6);
        for (var i = 0; i < 6; i++)
        {
            writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
            writer.Write((ushort)InputEventCode.KEY_A);
            writer.Write(i % 2);
            writer.Write(1_000_000L);
        }
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(6060, reader.ReadInt32());
        Assert.Contains("total delay", reader.ReadString(), StringComparison.Ordinal);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.SentEvents);
    }

    [LinuxFact]
    public async Task RunAsync_WhenSimulateEventPayloadIsMalformed_ShouldFailClosedStopCaptureAndSendNoPartialVirtualEvent()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1020, pid: 2030, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);
        SendStartCaptureCommand(reader, writer, requestId: 2020);

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
        writer.Write((byte)0x01);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.Empty(security.SimulationCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenConfigureResolutionThrows_ShouldFailClosedAndStopCapture()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager
        {
            ThrowOnReconfigure = new InvalidOperationException("resolution rejected"),
        };
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1017, pid: 2027, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1717);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1717, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.ConfigureResolution);
        writer.Write(1920);
        writer.Write(1080);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal([(0, 0), (1920, 1080)], virtualDevice.ConfigureCalls);
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(1));
    }

    [LinuxFact]
    public async Task RunAsync_WhenSimulateEventThrows_ShouldFailClosedAndStopCapture()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager
        {
            ThrowOnSendEvent = new InvalidOperationException("send failed"),
        };
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1018, pid: 2028, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1818);
        writer.Write(value: true);
        writer.Write(value: false);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1818, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT);
        writer.Write(1);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        _ = Assert.Single(virtualDevice.SentEvents);
        Assert.Equal((CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, 1), virtualDevice.SentEvents[0]);
        Assert.Empty(security.SimulationCalls);
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(1));
    }
}
