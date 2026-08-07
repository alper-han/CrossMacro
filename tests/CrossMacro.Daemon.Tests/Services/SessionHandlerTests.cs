
namespace CrossMacro.Daemon.Tests.Services;

public sealed partial class SessionHandlerTests
{



























    private sealed class FakeSecurityService : ISecurityService
    {
        public int CaptureStartCalls { get; private set; }
        public int CaptureStopCalls { get; private set; }
        public List<(uint Uid, int Pid, ushort Type, ushort Code, int Value)> SimulationCalls { get; } = [];

        public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client, CancellationToken cancellationToken = default) =>
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

        public Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConfigureCalls.Add((width, height));

            if (width is 0 && height is 0 && ThrowOnInitialConfigure is not null)
            {
                throw ThrowOnInitialConfigure;
            }

            if ((width is not 0 || height is not 0) && ThrowOnReconfigure is not null)
            {
                throw ThrowOnReconfigure;
            }

            return Task.CompletedTask;
        }

        public Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SentEvents.Add((type, code, value));

            if (ThrowOnSendEvent is not null)
            {
                throw ThrowOnSendEvent;
            }

            return Task.CompletedTask;
        }

        public Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var inputEvent in events)
            {
                SentEvents.Add((inputEvent.Type, inputEvent.Code, inputEvent.Value));
            }

            return Task.CompletedTask;
        }

        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeInputCaptureManager : IInputCaptureManager
    {
        private readonly Lock _sync = new();
        private Action<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>? _onEvent;
        private readonly TaskCompletionSource _captureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<StopCaptureWaiter> _stopCaptureWaiters = [];
        private bool _emitDuringStart;
        private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _emitDuringStartEvent;
        private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event[]? _emitSequenceDuringStart;
        private bool _emitPreviousAndCurrentOnNextStart;
        private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _previousGenerationStartEvent;
        private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _currentGenerationStartEvent;
        private CaptureStartResult _startResult = CaptureStartResult.Started(startedDeviceCount: 1);
        private Exception? _startException;

        public int StartCaptureCalls { get; private set; }
        public int StopCaptureCalls { get; private set; }
        public bool LastCaptureMouse { get; private set; }
        public bool LastCaptureKeyboard { get; private set; }

        public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event> onEvent)
        {
            if (_startException is not null)
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
            if (_emitSequenceDuringStart is not null)
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
            _ = _captureStarted.TrySetResult();
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

        public void Emit(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
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
                    _ = _stopCaptureWaiters.Remove(waiter);
                }

                throw new TimeoutException(
                    $"Timed out waiting for StopCaptureCalls >= {expectedCount}. Current StopCaptureCalls={GetStopCaptureCalls()}.",
                    ex);
            }
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

        private sealed class StopCaptureWaiter(int expectedCount)
        {
            private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int ExpectedCount { get; } = expectedCount;

            public Task Task => _completion.Task;

            public void Complete() => _completion.TrySetResult();
        }

        public void ConfigureEmitDuringStart(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
        {
            _emitDuringStart = true;
            _emitDuringStartEvent = inputEvent;
        }

        public void ConfigureEmitSequenceDuringStart(params CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event[] events)
        {
            _emitSequenceDuringStart = events.Length is 0 ? null : events;
        }

        public void ConfigureEmitPreviousAndCurrentEventsOnNextStart(
            CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event previousGenerationEvent,
            CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event currentGenerationEvent)
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
            var path = TestSocketPaths.CreateShort("cm-session");
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

    private static void SendStartCaptureCommand(BinaryReader reader, BinaryWriter writer, int requestId)
    {
        writer.Write((byte)IpcOpCode.StartCapture);
        writer.Write(requestId);
        writer.Write(value: true);
        writer.Write(value: true);
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
