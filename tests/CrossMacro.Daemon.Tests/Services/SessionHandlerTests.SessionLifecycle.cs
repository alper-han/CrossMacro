namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{

    [LinuxFact]
    public async Task RunAsync_WhenVirtualDeviceInitializationFails_ShouldReturnErrorAndExitFailClosed()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager
        {
            ThrowOnInitialConfigure = new InvalidOperationException("uinput unavailable"),
        };
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1012, pid: 2022, cts.Token);
        using var clientStream = new NetworkStream(socketPair.Client, ownsSocket: false);
        clientStream.ReadTimeout = 2000;
        using var reader = new BinaryReader(clientStream);
        using var writer = new BinaryWriter(clientStream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());
        Assert.Equal(IpcOpCode.Error, (IpcOpCode)reader.ReadByte());
        Assert.Contains("Failed to init UInput", reader.ReadString(), StringComparison.Ordinal);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        _ = Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Equal(0, captureManager.StartCaptureCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenCommandsAreReceived_ShouldDispatchToManagers()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1001, pid: 4321, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 2000;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(101);
        writer.Write(value: true);
        writer.Write(value: false);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(101, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.ConfigureResolution);
        writer.Write(1920);
        writer.Write(1080);
        writer.Flush();

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY);
        writer.Write(CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT);
        writer.Write(1);
        writer.Flush();

        writer.Write((byte)IpcOpCode.StopCapture);
        writer.Flush();

        await captureManager.WaitForStopCaptureCountAsync(expectedCount: 1, TimeSpan.FromSeconds(2));

        cts.Cancel();
        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Contains((1920, 1080), virtualDevice.ConfigureCalls);
        Assert.Contains((CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, 1), virtualDevice.SentEvents);

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.LastCaptureMouse);
        Assert.False(captureManager.LastCaptureKeyboard);
        Assert.True(captureManager.StopCaptureCalls >= 1);

        Assert.Equal(1, security.CaptureStartCalls);
        Assert.Equal(1, security.CaptureStopCalls);
        Assert.Contains(
            security.SimulationCalls,
            call =>
                call.Uid is 1001u && call.Pid is 4321 && call.Type == CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY && call.Code == CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT && call.Value is 1);
    }
}
