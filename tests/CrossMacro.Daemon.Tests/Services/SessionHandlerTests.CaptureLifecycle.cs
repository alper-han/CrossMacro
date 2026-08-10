namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{

    [LinuxFact]
    public async Task RunAsync_WhenSessionCommandPayloadIsMalformed_ShouldStopCaptureAndExit()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1013, pid: 2023, cts.Token);
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
        writer.Write(909);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(909, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.ConfigureResolution);
        writer.Write(1920);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cts.Token);

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        _ = Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
    }

    [LinuxFact]
    public async Task RunAsync_WhenStartCapturePayloadIsMalformed_ShouldFailClosedWithoutCaptureStartAudit()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1019, pid: 2029, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1919);
        writer.Write(value: true);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cts.Token);

        Assert.Equal([(0, 0)], virtualDevice.ConfigureCalls);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenCancellationOccursWhileWaitingForOpcode_ShouldCompletePromptlyAndStopCapture()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1021, pid: 2031, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);
        SendStartCaptureCommand(reader, writer, requestId: 2121);

        cts.Cancel();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
    }

    [LinuxFact]
    public async Task RunAsync_WhenClientDisconnectsAfterCaptureStarted_ShouldCompleteAndStopCapture()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1022, pid: 2032, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);
        SendStartCaptureCommand(reader, writer, requestId: 2222);

        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
    }

    [LinuxFact]
    public async Task RunAsync_WhenUndefinedOpcodeIsReceived_ShouldStopCaptureAndTerminateSession()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1023, pid: 2033, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)0x7F);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(2));

        Assert.Equal([(0, 0)], virtualDevice.ConfigureCalls);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenCaptureManagerEmitsInput_ShouldForwardInputEventToClient()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1002, pid: 9876, cts.Token);
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
        writer.Write(202);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(202, reader.ReadInt32());

        captureManager.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT,
            value = 1,
        });

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenCaptureManagerEmitsDuringStartup_ShouldSendCaptureStartedBeforeInputEvents()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        captureManager.ConfigureEmitDuringStart(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT,
            value = 1,
        });
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1004, pid: 6543, cts.Token);
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
        writer.Write(404);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(404, reader.ReadInt32());

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenCaptureManagerStartFails_ShouldSendCaptureStartFailedResponse()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        captureManager.ConfigureStartFailure("No matching input devices found.");
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1006, pid: 1111, cts.Token);
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
        writer.Write(606);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStartFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(606, reader.ReadInt32());
        Assert.Contains("No matching input devices found", reader.ReadString(), StringComparison.Ordinal);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenCaptureManagerThrowsDuringStart_ShouldSendCaptureStartFailedResponse()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        captureManager.ConfigureStartException(new InvalidOperationException("boom from capture manager"));
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1008, pid: 3333, cts.Token);
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
        writer.Write(808);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStartFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(808, reader.ReadInt32());
        Assert.Contains("internal error", reader.ReadString(), StringComparison.OrdinalIgnoreCase);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenPendingStartupBufferExceedsLimit_ShouldDropOldestEvents()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        captureManager.ConfigureEmitSequenceDuringStart(
            new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 10, value = 1 },
            new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 11, value = 1 },
            new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 12, value = 1 });
        var handler = new SessionHandler(security, virtualDevice, captureManager, maxBufferedCaptureEvents: 2);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1007, pid: 2222, cts.Token);
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
        writer.Write(707);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(707, reader.ReadInt32());

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Key, reader.ReadByte());
        Assert.Equal(11, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Key, reader.ReadByte());
        Assert.Equal(12, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenReconfiguringCapture_ShouldNotReplayPreviousGenerationEventsAfterAck()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1005, pid: 7654, cts.Token);
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
        writer.Write(501);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(501, reader.ReadInt32());

        captureManager.ConfigureEmitPreviousAndCurrentEventsOnNextStart(
            previousGenerationEvent: new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
            {
                type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
                code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT,
                value = 1,
            },
            currentGenerationEvent: new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
            {
                type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
                code = 30,
                value = 1,
            });

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(502);
        writer.Write(value: false);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(502, reader.ReadInt32());

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Key, reader.ReadByte());
        Assert.Equal(30, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenStoppingCapture_DropsLateEventsFromPreviousGeneration()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1014, pid: 2024, cts.Token);
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
        writer.Write(1001);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1001, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StopCapture);
        writer.Flush();

        await captureManager.WaitForStopCaptureCountAsync(expectedCount: 1, TimeSpan.FromSeconds(2));

        captureManager.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT,
            value = 1,
        });

        AssertNoMessageAvailable(stream, reader, TimeSpan.FromMilliseconds(200));

        captureManager.ConfigureEmitDuringStart(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = 30,
            value = 1,
        });

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1002);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1002, reader.ReadInt32());

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Key, reader.ReadByte());
        Assert.Equal(30, reader.ReadInt32());
        Assert.Equal(1, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenStoppingCaptureBeforeStart_ShouldBeSafe()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1016, pid: 2026, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StopCapture);
        writer.Flush();

        await captureManager.WaitForStopCaptureCountAsync(expectedCount: 1, TimeSpan.FromSeconds(2));

        _ = Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(1, security.CaptureStopCalls);

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1017);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1017, reader.ReadInt32());

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenCaptureManagerEmitsUnknownInputType_ShouldForwardUnknownEventType()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1003, pid: 5432, cts.Token);
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
        writer.Write(303);
        writer.Write(value: true);
        writer.Write(value: true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(303, reader.ReadInt32());

        captureManager.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = 0x99,
            code = 123,
            value = 77,
        });

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Unknown, reader.ReadByte());
        Assert.Equal(123, reader.ReadInt32());
        Assert.Equal(77, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
