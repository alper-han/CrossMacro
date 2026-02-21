using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Core.Services;
using CrossMacro.Daemon.Services;
using CrossMacro.Platform.Linux.Native.UInput;
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
        writer.Write(true);
        writer.Write(false);
        writer.Flush();

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
        writer.Write(true);
        writer.Write(true);
        writer.Flush();

        await captureManager.WaitForStartCaptureAsync(TimeSpan.FromSeconds(2));

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

    private sealed class FakeSecurityService : ISecurityService
    {
        public int CaptureStartCalls { get; private set; }
        public int CaptureStopCalls { get; private set; }

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
    }

    private sealed class FakeVirtualDeviceManager : IVirtualDeviceManager
    {
        public List<(int Width, int Height)> ConfigureCalls { get; } = [];
        public List<(ushort Type, ushort Code, int Value)> SentEvents { get; } = [];

        public void Configure(int width, int height)
        {
            ConfigureCalls.Add((width, height));
        }

        public void SendEvent(ushort type, ushort code, int value)
        {
            SentEvents.Add((type, code, value));
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
        private Action<UInputNative.input_event>? _onEvent;
        private readonly TaskCompletionSource _captureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCaptureCalls { get; private set; }
        public int StopCaptureCalls { get; private set; }
        public bool LastCaptureMouse { get; private set; }
        public bool LastCaptureKeyboard { get; private set; }

        public void StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
        {
            StartCaptureCalls++;
            LastCaptureMouse = captureMouse;
            LastCaptureKeyboard = captureKeyboard;
            _onEvent = onEvent;
            _captureStarted.TrySetResult();
        }

        public void StopCapture()
        {
            StopCaptureCalls++;
        }

        public void Emit(UInputNative.input_event inputEvent)
        {
            _onEvent?.Invoke(inputEvent);
        }

        public Task WaitForStartCaptureAsync(TimeSpan timeout) =>
            _captureStarted.Task.WaitAsync(timeout);

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
}
