namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcTransportWriteTests
{
    private const int OversizedPayloadBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan ShortWriteTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(5);

    [LinuxFact]
    public async Task Send_WhenDaemonStopsReading_FailsWithinWriteTimeoutAndDropsSession()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.StopReadingAfterHandshake);
        var callbacks = new RecordingHandlers();
        using var transport = CreateTransport(socketPath, callbacks, ShortWriteTimeout);
        await transport.ConnectAsync(CancellationToken.None);

        var send = Task.Run(
            () => transport.Send(IpcOpCode.SimulateEvent, WriteOversizedPayload, throwOnFailure: true),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            send.WaitAsync(TestBudget, TimeProvider.System, CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);
        Assert.NotNull(exception.InnerException);
        Assert.True(IpcTransport.IsTimeoutException(exception.InnerException));
        Assert.False(transport.IsConnected);
        Assert.Equal(1, callbacks.SendFailures);
    }

    [LinuxFact]
    public async Task SendAsync_WhenDaemonStopsReading_FailsWithinWriteTimeoutAndDropsSession()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.StopReadingAfterHandshake);
        var callbacks = new RecordingHandlers();
        using var transport = CreateTransport(socketPath, callbacks, ShortWriteTimeout);
        await transport.ConnectAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            transport.SendAsync(
                IpcOpCode.SimulateEvent,
                WriteOversizedPayload,
                throwOnFailure: true,
                CancellationToken.None)
                .WaitAsync(TestBudget, TimeProvider.System, CancellationToken.None));

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.False(transport.IsConnected);
        Assert.Equal(1, callbacks.SendFailures);
    }

    [LinuxFact]
    public async Task Cleanup_WhileWriteIsBlockedOnStalledDaemon_DoesNotWaitForTheWrite()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.StopReadingAfterHandshake);
        var callbacks = new RecordingHandlers();
        using var transport = CreateTransport(socketPath, callbacks, TimeSpan.FromMinutes(1));
        await transport.ConnectAsync(CancellationToken.None);

        var send = Task.Run(
            () => transport.Send(IpcOpCode.SimulateEvent, WriteOversizedPayload),
            CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TimeProvider.System, CancellationToken.None);
        Assert.False(send.IsCompleted, "The write should be blocked on the stalled daemon.");

        var cleanup = Task.Run(
            () => transport.Cleanup(clearSubscriptions: true, disableReconnect: true),
            CancellationToken.None);

        await cleanup.WaitAsync(TestBudget, TimeProvider.System, CancellationToken.None);
        Assert.False(transport.IsConnected);
        Assert.False(await send.WaitAsync(TestBudget, TimeProvider.System, CancellationToken.None));
    }

    [LinuxFact]
    public async Task Send_WhenFrameWriterThrows_PropagatesAndKeepsSession()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        var callbacks = new RecordingHandlers();
        using var transport = CreateTransport(socketPath, callbacks, TestBudget);
        await transport.ConnectAsync(CancellationToken.None);

        _ = Assert.Throws<InvalidOperationException>(() =>
            transport.Send(IpcOpCode.SimulateEvent, static _ => throw new InvalidOperationException("serialization failed")));

        Assert.True(transport.IsConnected);
        Assert.Equal(0, callbacks.SendFailures);
        Assert.True(transport.Send(IpcOpCode.StopCapture));
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TestBudget);
        Assert.Equal(IpcOpCode.StopCapture, Assert.Single(daemon.GetCommandsSnapshot()).OpCode);
    }

    [LinuxFact]
    public async Task ConcurrentSyncAndAsyncSends_ArriveAsWholeFrames()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        var callbacks = new RecordingHandlers();
        using var transport = CreateTransport(socketPath, callbacks, TestBudget);
        await transport.ConnectAsync(CancellationToken.None);

        const int framesPerWriter = 200;
        var sync = Task.Run(
            () =>
            {
                for (var i = 0; i < framesPerWriter; i++)
                {
                    _ = transport.Send(IpcOpCode.ConfigureResolution, w => IpcMessageCodec.WriteResolutionPayload(w, 1, i), throwOnFailure: true);
                }
            },
            CancellationToken.None);
        var asyncSender = Task.Run(
            async () =>
            {
                for (var i = 0; i < framesPerWriter; i++)
                {
                    _ = await transport.SendAsync(
                        IpcOpCode.ConfigureResolution,
                        w => IpcMessageCodec.WriteResolutionPayload(w, 2, i),
                        throwOnFailure: true,
                        CancellationToken.None);
                }
            },
            CancellationToken.None);

        await Task.WhenAll(sync, asyncSender).WaitAsync(TestBudget, TimeProvider.System, CancellationToken.None);
        await daemon.WaitForCommandCountAsync(expected: framesPerWriter * 2, timeout: TestBudget);

        var commands = daemon.GetCommandsSnapshot();
        Assert.All(commands, static command => Assert.Equal(IpcOpCode.ConfigureResolution, command.OpCode));
        Assert.Equal(
            Enumerable.Range(0, framesPerWriter),
            commands.Where(static command => command.Width is 1).Select(static command => command.Height));
        Assert.Equal(
            Enumerable.Range(0, framesPerWriter),
            commands.Where(static command => command.Width is 2).Select(static command => command.Height));
    }

    private static void WriteOversizedPayload(BinaryWriter writer) => writer.Write(new byte[OversizedPayloadBytes]);

    private static IpcTransport CreateTransport(string socketPath, RecordingHandlers recorder, TimeSpan writeTimeout) =>
        new(() => socketPath, autoReconnect: false, recorder.Handlers, writeTimeout);

    private sealed class RecordingHandlers
    {
        private int _sendFailures;

        public RecordingHandlers()
        {
            Handlers = new IpcTransportHandlers(
                static (_, _) => { /* The tests never receive frames. */ },
                static _ => Task.CompletedTask,
                static () => { /* Not observed. */ },
                static _ => { /* Not observed. */ },
                static _ => { /* Not observed. */ },
                (_, _) => Interlocked.Increment(ref _sendFailures),
                static _ => { /* Not observed. */ });
        }

        public IpcTransportHandlers Handlers { get; }

        public int SendFailures => Volatile.Read(ref _sendFailures);
    }
}
