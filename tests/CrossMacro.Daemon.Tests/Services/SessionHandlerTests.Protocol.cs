namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{

    [LinuxFact]
    public async Task RunAsync_WhenProtocolVersionMismatch_ShouldReturnErrorAndExit()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1000, pid: 2000, cts.Token);
        using var clientStream = new NetworkStream(socketPair.Client, ownsSocket: false);
        clientStream.ReadTimeout = 2000;
        using var reader = new BinaryReader(clientStream);
        using var writer = new BinaryWriter(clientStream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion + 1);
        writer.Flush();

        var opcode = (IpcOpCode)reader.ReadByte();
        var message = reader.ReadString();

        Assert.Equal(IpcOpCode.Error, opcode);
        Assert.Contains("Protocol version mismatch", message, StringComparison.Ordinal);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenHandshakeOpcodeIsInvalid_ShouldFailClosedWithoutInitializingVirtualDevice()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1010, pid: 2020, cts.Token);
        using var clientStream = new NetworkStream(socketPair.Client, ownsSocket: false);
        clientStream.ReadTimeout = 500;
        using var reader = new BinaryReader(clientStream);
        using var writer = new BinaryWriter(clientStream);

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.ConfigureCalls);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        _ = Assert.ThrowsAny<IOException>(() => reader.ReadByte());
    }

    [LinuxFact]
    public async Task RunAsync_WhenHandshakePayloadIsMalformed_ShouldFailClosedWithoutInitializingVirtualDevice()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1011, pid: 2021, cts.Token);
        using var clientStream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var writer = new BinaryWriter(clientStream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write((byte)0x7F);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.ConfigureCalls);
        Assert.Equal(0, captureManager.StartCaptureCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenUnknownOpcodeIsReceived_ShouldTerminateSessionWithoutCommandSideEffects()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1015, pid: 2025, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write(byte.MaxValue);
        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1016);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(2));

        _ = Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);
    }
}
