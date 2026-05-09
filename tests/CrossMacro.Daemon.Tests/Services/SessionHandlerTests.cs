using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Daemon.Tests.Services;

public sealed class SessionHandlerTests
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
        Assert.ThrowsAny<IOException>(() => reader.ReadByte());
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
    public async Task RunAsync_WhenVirtualDeviceInitializationFails_ShouldReturnErrorAndExitFailClosed()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        virtualDevice.ThrowOnInitialConfigure = new InvalidOperationException("uinput unavailable");
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

        Assert.Single(virtualDevice.ConfigureCalls);
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
        writer.Write(true);
        writer.Write(false);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(101, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.ConfigureResolution);
        writer.Write(1920);
        writer.Write(1080);
        writer.Flush();

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write((ushort)UInputNative.EV_KEY);
        writer.Write((ushort)UInputNative.BTN_LEFT);
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
        Assert.Contains((UInputNative.EV_KEY, UInputNative.BTN_LEFT, 1), virtualDevice.SentEvents);

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.LastCaptureMouse);
        Assert.False(captureManager.LastCaptureKeyboard);
        Assert.True(captureManager.StopCaptureCalls >= 1);

        Assert.Equal(1, security.CaptureStartCalls);
        Assert.Equal(1, security.CaptureStopCalls);
        Assert.Contains(
            security.SimulationCalls,
            call =>
                call.Uid == 1001u &&
                call.Pid == 4321 &&
                call.Type == UInputNative.EV_KEY &&
                call.Code == UInputNative.BTN_LEFT &&
                call.Value == 1);
    }

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
        writer.Write((ushort)UInputNative.EV_KEY);
        writer.Write((ushort)InputEventCode.KEY_A);
        writer.Write(1);
        writer.Write(0);
        writer.Write((ushort)UInputNative.EV_SYN);
        writer.Write((ushort)UInputNative.SYN_REPORT);
        writer.Write(0);
        writer.Write(0);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchCompleted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(3030, reader.ReadInt32());

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            [(UInputNative.EV_KEY, (ushort)InputEventCode.KEY_A, 1), (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0)],
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

        StartCapture(reader, writer, requestId: 4041);

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
        writer.Write((ushort)UInputNative.EV_KEY);
        writer.Write((ushort)InputEventCode.KEY_A);
        writer.Write(1);
        writer.Write(-1);
        writer.Flush();

        Assert.Equal(IpcOpCode.SimulationBatchFailed, (IpcOpCode)reader.ReadByte());
        Assert.Equal(5050, reader.ReadInt32());
        Assert.Contains("delay", reader.ReadString(), StringComparison.Ordinal);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(virtualDevice.SentEvents);
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
            writer.Write((ushort)UInputNative.EV_KEY);
            writer.Write((ushort)InputEventCode.KEY_A);
            writer.Write(i % 2);
            writer.Write(1000);
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(909, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.ConfigureResolution);
        writer.Write(1920);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Single(virtualDevice.ConfigureCalls);
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
        writer.Write(true);
        writer.Flush();
        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal([(0, 0)], virtualDevice.ConfigureCalls);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);
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
        StartCapture(reader, writer, requestId: 2020);

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write((ushort)UInputNative.EV_KEY);
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
        StartCapture(reader, writer, requestId: 2121);

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
        StartCapture(reader, writer, requestId: 2222);

        socketPair.Client.Shutdown(SocketShutdown.Send);

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
    }

    [LinuxFact]
    public async Task RunAsync_WhenUndefinedOpcodeIsReceived_ShouldHaveNoResponseSideEffectsAndKeepSessionAlive()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1023, pid: 2033, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        CompleteHandshake(reader, writer);

        writer.Write((byte)0x7F);
        writer.Flush();

        AssertNoMessageAvailable(stream, reader, TimeSpan.FromMilliseconds(200));

        Assert.Equal([(0, 0)], virtualDevice.ConfigureCalls);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.Equal(0, captureManager.StopCaptureCalls);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);

        StartCapture(reader, writer, requestId: 2323);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(202, reader.ReadInt32());

        captureManager.Emit(new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = UInputNative.BTN_LEFT,
            value = 1
        });

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)UInputNative.BTN_LEFT, reader.ReadInt32());
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
        captureManager.ConfigureEmitDuringStart(new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = UInputNative.BTN_LEFT,
            value = 1
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(404, reader.ReadInt32());

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)UInputNative.BTN_LEFT, reader.ReadInt32());
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
        writer.Write(true);
        writer.Write(true);
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
        writer.Write(true);
        writer.Write(true);
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
            new UInputNative.input_event { type = UInputNative.EV_KEY, code = 10, value = 1 },
            new UInputNative.input_event { type = UInputNative.EV_KEY, code = 11, value = 1 },
            new UInputNative.input_event { type = UInputNative.EV_KEY, code = 12, value = 1 });
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
        writer.Write(true);
        writer.Write(true);
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(501, reader.ReadInt32());

        captureManager.ConfigureEmitPreviousAndCurrentEventsOnNextStart(
            previousGenerationEvent: new UInputNative.input_event
            {
                type = UInputNative.EV_KEY,
                code = UInputNative.BTN_LEFT,
                value = 1
            },
            currentGenerationEvent: new UInputNative.input_event
            {
                type = UInputNative.EV_KEY,
                code = 30,
                value = 1
            });

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(502);
        writer.Write(false);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.MouseButton, reader.ReadByte());
        Assert.Equal((int)UInputNative.BTN_LEFT, reader.ReadInt32());
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1001, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.StopCapture);
        writer.Flush();

        await captureManager.WaitForStopCaptureCountAsync(expectedCount: 1, TimeSpan.FromSeconds(2));

        captureManager.Emit(new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = UInputNative.BTN_LEFT,
            value = 1
        });

        AssertNoMessageAvailable(stream, reader, TimeSpan.FromMilliseconds(200));

        captureManager.ConfigureEmitDuringStart(new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = 30,
            value = 1
        });

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1002);
        writer.Write(true);
        writer.Write(true);
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
    public async Task RunAsync_WhenUnknownOpcodeIsReceived_ShouldHaveNoSideEffectsAndKeepSessionAlive()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var handler = new SessionHandler(security, virtualDevice, captureManager);

        await using var socketPair = await UnixSocketPair.CreateAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var runTask = StartSessionOnBackgroundThread(handler, socketPair.Server, uid: 1015, pid: 2025, cts.Token);
        using var stream = new NetworkStream(socketPair.Client, ownsSocket: false);
        stream.ReadTimeout = 750;
        using var reader = new BinaryReader(stream);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());

        writer.Write(byte.MaxValue);
        writer.Flush();

        AssertNoMessageAvailable(stream, reader, TimeSpan.FromMilliseconds(200));

        Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Empty(virtualDevice.SentEvents);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.Equal(0, captureManager.StopCaptureCalls);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(0, security.CaptureStopCalls);

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1015);
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1015, reader.ReadInt32());

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

        Assert.Single(virtualDevice.ConfigureCalls);
        Assert.Equal((0, 0), virtualDevice.ConfigureCalls[0]);
        Assert.Equal(0, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Equal(0, security.CaptureStartCalls);
        Assert.Equal(1, security.CaptureStopCalls);

        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(1017);
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1017, reader.ReadInt32());

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task RunAsync_WhenConfigureResolutionThrows_ShouldFailClosedAndStopCapture()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager
        {
            ThrowOnReconfigure = new InvalidOperationException("resolution rejected")
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
        writer.Write(true);
        writer.Write(true);
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
            ThrowOnSendEvent = new InvalidOperationException("send failed")
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
        writer.Write(true);
        writer.Write(false);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(1818, reader.ReadInt32());

        writer.Write((byte)IpcOpCode.SimulateEvent);
        writer.Write((ushort)UInputNative.EV_KEY);
        writer.Write((ushort)UInputNative.BTN_LEFT);
        writer.Write(1);
        writer.Flush();

        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, captureManager.StartCaptureCalls);
        Assert.True(captureManager.StopCaptureCalls >= 1);
        Assert.Single(virtualDevice.SentEvents);
        Assert.Equal((UInputNative.EV_KEY, UInputNative.BTN_LEFT, 1), virtualDevice.SentEvents[0]);
        Assert.Empty(security.SimulationCalls);
        await AssertRemoteClosedAsync(stream, TimeSpan.FromSeconds(1));
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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(303, reader.ReadInt32());

        captureManager.Emit(new UInputNative.input_event
        {
            type = 0x99,
            code = 123,
            value = 77
        });

        Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)reader.ReadByte());
        Assert.Equal((byte)InputEventType.Unknown, reader.ReadByte());
        Assert.Equal(123, reader.ReadInt32());
        Assert.Equal(77, reader.ReadInt32());
        Assert.True(reader.ReadInt64() > 0);

        socketPair.Client.Dispose();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class FakeSecurityService : ISecurityService
    {
        public int CaptureStartCalls { get; private set; }
        public int CaptureStopCalls { get; private set; }
        public List<(uint Uid, int Pid, ushort Type, ushort Code, int Value)> SimulationCalls { get; } = [];

        public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client) =>
            Task.FromResult<(uint Uid, int Pid)?>(null);

        public void LogDisconnect(uint uid, int pid, TimeSpan duration)
        {
        }

        public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb)
        {
            CaptureStartCalls++;
        }

        public void LogCaptureStop(uint uid, int pid)
        {
            CaptureStopCalls++;
        }

        public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
        {
            SimulationCalls.Add((uid, pid, type, code, value));
        }
    }

    private sealed class FakeVirtualDeviceManager : IVirtualDeviceManager
    {
        public Exception? ThrowOnInitialConfigure { get; set; }
        public Exception? ThrowOnReconfigure { get; set; }
        public Exception? ThrowOnSendEvent { get; set; }
        public List<(int Width, int Height)> ConfigureCalls { get; } = [];
        public List<(ushort Type, ushort Code, int Value)> SentEvents { get; } = [];

        public void Configure(int width, int height)
        {
            ConfigureCalls.Add((width, height));

            if (width == 0 && height == 0 && ThrowOnInitialConfigure != null)
            {
                throw ThrowOnInitialConfigure;
            }

            if ((width != 0 || height != 0) && ThrowOnReconfigure != null)
            {
                throw ThrowOnReconfigure;
            }
        }

        public void SendEvent(ushort type, ushort code, int value)
        {
            SentEvents.Add((type, code, value));

            if (ThrowOnSendEvent != null)
            {
                throw ThrowOnSendEvent;
            }
        }

        public void SendEvents(ReadOnlySpan<IpcSimulationRequest> events)
        {
            foreach (var inputEvent in events)
            {
                SendEvent(inputEvent.Type, inputEvent.Code, inputEvent.Value);
            }
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeInputCaptureManager : IInputCaptureManager
    {
        private readonly object _sync = new();
        private Action<UInputNative.input_event>? _onEvent;
        private readonly TaskCompletionSource _captureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<StopCaptureWaiter> _stopCaptureWaiters = [];
        private bool _emitDuringStart;
        private UInputNative.input_event _emitDuringStartEvent;
        private UInputNative.input_event[]? _emitSequenceDuringStart;
        private bool _emitPreviousAndCurrentOnNextStart;
        private UInputNative.input_event _previousGenerationStartEvent;
        private UInputNative.input_event _currentGenerationStartEvent;
        private CaptureStartResult _startResult = CaptureStartResult.Started(startedDeviceCount: 1);
        private Exception? _startException;

        public int StartCaptureCalls { get; private set; }
        public int StopCaptureCalls { get; private set; }
        public bool LastCaptureMouse { get; private set; }
        public bool LastCaptureKeyboard { get; private set; }

        public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
        {
            if (_startException != null)
            {
                var exception = _startException;
                _startException = null;
                throw exception;
            }

            StartCaptureCalls++;
            LastCaptureMouse = captureMouse;
            LastCaptureKeyboard = captureKeyboard;
            var previousOnEvent = _onEvent;
            _onEvent = onEvent;
            if (_emitDuringStart)
            {
                onEvent(_emitDuringStartEvent);
            }
            if (_emitSequenceDuringStart != null)
            {
                foreach (var inputEvent in _emitSequenceDuringStart)
                {
                    onEvent(inputEvent);
                }

                _emitSequenceDuringStart = null;
            }
            if (_emitPreviousAndCurrentOnNextStart)
            {
                previousOnEvent?.Invoke(_previousGenerationStartEvent);
                onEvent(_currentGenerationStartEvent);
                _emitPreviousAndCurrentOnNextStart = false;
            }
            _captureStarted.TrySetResult();
            var startResult = _startResult;
            _startResult = CaptureStartResult.Started(startedDeviceCount: 1);
            return startResult;
        }

        public void StopCapture()
        {
            List<StopCaptureWaiter> completedWaiters;
            lock (_sync)
            {
                StopCaptureCalls++;
                completedWaiters = CompleteSatisfiedStopCaptureWaiters();
            }

            foreach (var waiter in completedWaiters)
            {
                waiter.Complete();
            }
        }

        public void Emit(UInputNative.input_event inputEvent)
        {
            _onEvent?.Invoke(inputEvent);
        }

        public Task WaitForStartCaptureAsync(TimeSpan timeout) =>
            _captureStarted.Task.WaitAsync(timeout);

        public async Task WaitForStopCaptureCountAsync(int expectedCount, TimeSpan timeout)
        {
            StopCaptureWaiter? waiter = null;
            lock (_sync)
            {
                if (StopCaptureCalls >= expectedCount)
                {
                    return;
                }

                waiter = new StopCaptureWaiter(expectedCount);
                _stopCaptureWaiters.Add(waiter);
            }

            try
            {
                await waiter.Task.WaitAsync(timeout);
            }
            catch (TimeoutException ex)
            {
                lock (_sync)
                {
                    _stopCaptureWaiters.Remove(waiter);
                }

                throw new TimeoutException(
                    $"Timed out waiting for StopCaptureCalls >= {expectedCount}. Current StopCaptureCalls={GetStopCaptureCalls()}.",
                    ex);
            }
        }

        public Task WaitForStopCaptureAsync(TimeSpan timeout)
        {
            var expectedCount = GetStopCaptureCalls() + 1;
            return WaitForStopCaptureCountAsync(expectedCount, timeout);
        }

        private int GetStopCaptureCalls()
        {
            lock (_sync)
            {
                return StopCaptureCalls;
            }
        }

        private List<StopCaptureWaiter> CompleteSatisfiedStopCaptureWaiters()
        {
            var completedWaiters = new List<StopCaptureWaiter>();
            for (var index = _stopCaptureWaiters.Count - 1; index >= 0; index--)
            {
                var waiter = _stopCaptureWaiters[index];
                if (StopCaptureCalls < waiter.ExpectedCount)
                {
                    continue;
                }

                _stopCaptureWaiters.RemoveAt(index);
                completedWaiters.Add(waiter);
            }

            return completedWaiters;
        }

        private sealed class StopCaptureWaiter
        {
            private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public StopCaptureWaiter(int expectedCount)
            {
                ExpectedCount = expectedCount;
            }

            public int ExpectedCount { get; }

            public Task Task => _completion.Task;

            public void Complete() => _completion.TrySetResult();
        }

        public void ConfigureEmitDuringStart(UInputNative.input_event inputEvent)
        {
            _emitDuringStart = true;
            _emitDuringStartEvent = inputEvent;
        }

        public void ConfigureEmitSequenceDuringStart(params UInputNative.input_event[] events)
        {
            _emitSequenceDuringStart = events.Length == 0 ? null : events;
        }

        public void ConfigureEmitPreviousAndCurrentEventsOnNextStart(
            UInputNative.input_event previousGenerationEvent,
            UInputNative.input_event currentGenerationEvent)
        {
            _emitPreviousAndCurrentOnNextStart = true;
            _previousGenerationStartEvent = previousGenerationEvent;
            _currentGenerationStartEvent = currentGenerationEvent;
        }

        public void ConfigureStartFailure(string errorMessage)
        {
            _startResult = CaptureStartResult.Failed(errorMessage);
        }

        public void ConfigureStartException(Exception exception)
        {
            _startException = exception;
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnixSocketPair : IAsyncDisposable
    {
        private readonly Socket _listener;
        private readonly string _path;
        public Socket Client { get; }
        public Socket Server { get; }

        private UnixSocketPair(Socket listener, string path, Socket client, Socket server)
        {
            _listener = listener;
            _path = path;
            Client = client;
            Server = server;
        }

        public static async Task<UnixSocketPair> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"crossmacro-daemon-session-{Guid.NewGuid():N}.sock");
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);

            var acceptTask = listener.AcceptAsync();

            var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await client.ConnectAsync(new UnixDomainSocketEndPoint(path));
            var server = await acceptTask;

            return new UnixSocketPair(listener, path, client, server);
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            Server.Dispose();
            _listener.Dispose();

            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return ValueTask.CompletedTask;
        }
    }

    private static Task StartSessionOnBackgroundThread(
        SessionHandler handler,
        Socket server,
        uint uid,
        int pid,
        CancellationToken token)
    {
        return Task.Factory.StartNew(
            () => handler.RunAsync(server, uid, pid, token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private static void CompleteHandshake(BinaryReader reader, BinaryWriter writer)
    {
        writer.Write((byte)IpcOpCode.Handshake);
        writer.Write(IpcProtocol.ProtocolVersion);
        writer.Flush();

        Assert.Equal(IpcOpCode.Handshake, (IpcOpCode)reader.ReadByte());
        Assert.Equal(IpcProtocol.ProtocolVersion, reader.ReadInt32());
    }

    private static void StartCapture(BinaryReader reader, BinaryWriter writer, int requestId)
    {
        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(requestId);
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)reader.ReadByte());
        Assert.Equal(requestId, reader.ReadInt32());
    }

    private static void AssertNoMessageAvailable(NetworkStream stream, BinaryReader reader, TimeSpan timeout)
    {
        var previousTimeout = stream.ReadTimeout;
        stream.ReadTimeout = (int)timeout.TotalMilliseconds;

        try
        {
            var exception = Assert.Throws<IOException>(() => reader.ReadByte());
            Assert.True(
                exception.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut } ||
                exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase),
                $"Expected a read timeout while the session remained open, but got: {exception}");
        }
        finally
        {
            stream.ReadTimeout = previousTimeout;
        }
    }

    private static async Task AssertRemoteClosedAsync(NetworkStream stream, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var buffer = new byte[1];

        try
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cts.Token);
            Assert.Equal(0, bytesRead);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for the session handler to close the client stream.", ex);
        }
        catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.NotConnected })
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
