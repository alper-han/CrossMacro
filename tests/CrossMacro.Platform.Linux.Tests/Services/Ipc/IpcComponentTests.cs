using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

/// <summary>Gate ordering and failure routing of the transport and capture controller, driven without the client facade.</summary>
public sealed class IpcComponentTests
{
    private static readonly TimeSpan AsyncOperationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ConnectAsync_WhenDisposedWhileWaitingForConnectGate_CannotInstallTransport()
    {
        var resolverCalls = 0;
        await using var fixture = new IpcComponentFixture("/tmp/unused.sock");
        using var transport = new IpcTransport(
            () =>
            {
                _ = Interlocked.Increment(ref resolverCalls);
                throw new InvalidOperationException("Socket resolver should not run.");
            },
            autoReconnect: false,
            CreateNoOpHandlers(),
            IpcTransport.DefaultWriteTimeout);
        Assert.True(await transport.ConnectGate.WaitAsync(CommandTimeout, CancellationToken.None));

        var connectTask = transport.ConnectAsync(CancellationToken.None);
        Assert.False(connectTask.IsCompleted);
        transport.MarkDisposed();
        _ = transport.ConnectGate.Release();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            connectTask.WaitAsync(CommandTimeout, TimeProvider.System, CancellationToken.None));
        Assert.Equal(0, Volatile.Read(ref resolverCalls));
    }

    [Fact]
    public async Task PublishAfterCommands_WhenTransportIsDisposed_DoesNotInvokePublisher()
    {
        await using var fixture = new IpcComponentFixture("/tmp/unused.sock");
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(await fixture.Capture.CommandGate.WaitAsync(CommandTimeout, CancellationToken.None));

        fixture.Capture.PublishAfterCommands(() => published.TrySetResult());
        fixture.Transport.MarkDisposed();
        await fixture.Transport.ShutdownAsync();
        _ = fixture.Capture.CommandGate.Release();

        await Task.Delay(TimeSpan.FromMilliseconds(200), TimeProvider.System, CancellationToken.None);
        Assert.False(published.Task.IsCompleted);
    }

    [Fact]
    public async Task OnSendFailure_WhenErrorHandlerReentersCaptureControl_ShouldNotBlockCaller()
    {
        await using var fixture = new IpcComponentFixture("/tmp/non-existent.sock");
        var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ErrorReported += reportedMessage =>
        {
            _ = callbackObserved.TrySetResult();
            fixture.Capture.StopCapture("reentrant-consumer");
        };

        await ReportSendFailureWhileHoldingGateAsync(fixture, new IOException("Simulated send failure"), callbackObserved.Task);

        await callbackObserved.Task.WaitAsync(CommandTimeout, TimeProvider.System, CancellationToken.None);
    }

    [Fact]
    public async Task OnSendFailure_WhenReenteredRepeatedly_ShouldNotDeadlock()
    {
        await using var fixture = new IpcComponentFixture("/tmp/non-existent.sock");

        const int iterations = 50;
        var callbacksObserved = 0;
        TaskCompletionSource? nextCallbackObserved = null;
        fixture.ErrorReported += reportedMessage =>
        {
            _ = Interlocked.Increment(ref callbacksObserved);
            _ = Volatile.Read(ref nextCallbackObserved)?.TrySetResult();

            fixture.Capture.StartCapture("stress-consumer", mouse: true, keyboard: true);
            fixture.Capture.StopCapture("stress-consumer");
        };

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref nextCallbackObserved, callbackObserved);

            await ReportSendFailureWhileHoldingGateAsync(
                fixture,
                new IOException(string.Create(CultureInfo.InvariantCulture, $"Simulated send failure {iteration}")),
                pendingCallback: null);

            await callbackObserved.Task.WaitAsync(CommandTimeout, TimeProvider.System, CancellationToken.None);
            Assert.Equal(iteration + 1, Volatile.Read(ref callbacksObserved));
        }

        Assert.Equal(iterations, Volatile.Read(ref callbacksObserved));
    }

    [Fact]
    public async Task DisposeAsync_WhenDeferredReconcileIsWaitingForGate_ShouldCancelWithoutLeakingCancellation()
    {
        var fixture = new IpcComponentFixture("/tmp/non-existent.sock");
        Assert.True(await fixture.Capture.CommandGate.WaitAsync(CommandTimeout, CancellationToken.None));

        var reconcileTask = fixture.Capture.StartDeferredCaptureReconcileAsync();
        await fixture.DisposeAsync().AsTask().WaitAsync(CommandTimeout, TimeProvider.System, CancellationToken.None);

        var exception = await Record.ExceptionAsync(() =>
            reconcileTask.WaitAsync(CommandTimeout, TimeProvider.System, CancellationToken.None));
        Assert.Null(exception);
    }

    [Fact]
    public async Task DeferredCaptureReconcile_WhenTransportDropsBeforeGateReleases_ShouldCancelWithoutIssuingStaleCommand()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.DelayAllCaptureStartAcks);
        await using var fixture = new IpcComponentFixture(socketPath);

        await fixture.Transport.ConnectAsync(CancellationToken.None);
        await fixture.Capture.StartCaptureAsync("consumer-a", mouse: false, keyboard: true, CancellationToken.None)
            .WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: CommandTimeout);

        fixture.Capture.StartCapture("consumer-b", mouse: true, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: CommandTimeout);

        var captureGate = fixture.Capture.CommandGate;
        Assert.True(
            await captureGate.WaitAsync(CommandTimeout, CancellationToken.None),
            "Timed out waiting to acquire the capture command gate.");

        // StopCapture blocks on the gate before doing anything else, so the dedicated thread
        // reaching WaitSleepJoin means it is queued behind the gate.
        Thread? stopCaptureThread = null;
        var stopCaptureTask = Task.Factory.StartNew(
            () =>
            {
                Volatile.Write(ref stopCaptureThread, Thread.CurrentThread);
                fixture.Capture.StopCapture("consumer-b");
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        try
        {
            Assert.True(
                SpinWait.SpinUntil(
                    () => Volatile.Read(ref stopCaptureThread) is { } worker
                        && worker.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin),
                    AsyncOperationTimeout),
                "StopCapture did not begin waiting on the capture command gate.");

            await daemon.DisposeAsync();
            _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                stopCaptureTask.WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None));
        }
        finally
        {
            _ = captureGate.Release();
        }

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(2, commands.Length);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.Equal(IpcOpCode.StartCapture, commands[1].OpCode);
    }

    [Fact]
    public async Task ConnectAsync_WhenReplayFindsPendingCaptureStart_ShouldReissueAndCompleteOriginalWaiter()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var fixture = new IpcComponentFixture(socketPath);

        fixture.Capture.StartCapture("global-hotkeys", mouse: true, keyboard: false);
        var stalePending = fixture.Capture.PendingCaptureStarts.Begin(
            new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true, CaptureKeyboard: false),
            notifyOnFailure: true);

        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);

        await fixture.Transport.ConnectAsync(CancellationToken.None)
            .WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);

        _ = await stalePending.Completion.Task.WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: CommandTimeout);

        var command = Assert.Single(daemon.GetCommandsSnapshot());
        Assert.Equal(IpcOpCode.StartCapture, command.OpCode);
        Assert.True(command.CaptureMouse);
        Assert.False(command.CaptureKeyboard);
        Assert.NotEqual(stalePending.RequestId, command.RequestId);
    }

    [Fact]
    public async Task SyncAndAsyncSends_ShareOneWriteGate()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        await using var fixture = new IpcComponentFixture(socketPath);
        await fixture.Transport.ConnectAsync(CancellationToken.None);

        var writeGate = fixture.Transport.WriteGate;
        Assert.True(await writeGate.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        try
        {
            var syncSend = Task.Run(
                () => fixture.Transport.Send(
                    IpcOpCode.SimulateEvent,
                    static writer =>
                    {
                        writer.Write((ushort)1);
                        writer.Write((ushort)30);
                        writer.Write(1);
                    },
                    throwOnFailure: true),
                CancellationToken.None);
            var asyncSend = fixture.Transport.SendAsync(
                IpcOpCode.StopCapture,
                payload: null,
                throwOnFailure: true,
                CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, CancellationToken.None);
            Assert.Empty(daemon.GetCommandsSnapshot());
            _ = writeGate.Release();

            await Task.WhenAll(syncSend, asyncSend).WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);
        }
        finally
        {
            if (writeGate.CurrentCount is 0)
            {
                _ = writeGate.Release();
            }
        }

        await daemon.WaitForCommandCountAsync(expected: 2, timeout: CommandTimeout);

        // SemaphoreSlim gives no FIFO guarantee, so assert the set of commands, not their order.
        var opCodes = daemon.GetCommandsSnapshot().Select(static command => command.OpCode).ToArray();
        Assert.Contains(IpcOpCode.SimulateEvent, opCodes);
        Assert.Contains(IpcOpCode.StopCapture, opCodes);
    }

    [Fact]
    public async Task StartCaptureAsync_WhenCanceledWhileWaitingForWriteGate_DoesNotSendAndReleasesCaptureState()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        await using var fixture = new IpcComponentFixture(socketPath);
        await fixture.Transport.ConnectAsync(CancellationToken.None);

        var writeGate = fixture.Transport.WriteGate;
        Assert.True(await writeGate.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
        try
        {
            using var cancellation = new CancellationTokenSource();
            var canceledStart = fixture.Capture.StartCaptureAsync(
                "canceled-write-gate",
                mouse: true,
                keyboard: false,
                cancellation.Token);

            await cancellation.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                canceledStart.WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None));

            Assert.Empty(daemon.GetCommandsSnapshot());
            _ = writeGate.Release();

            await fixture.Capture.StartCaptureAsync(
                "successful-write-gate",
                mouse: true,
                keyboard: false,
                CancellationToken.None)
                .WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);
        }
        finally
        {
            if (writeGate.CurrentCount is 0)
            {
                _ = writeGate.Release();
            }
        }

        await daemon.WaitForCommandCountAsync(expected: 1, timeout: CommandTimeout);
        var command = Assert.Single(daemon.GetCommandsSnapshot());
        Assert.Equal(IpcOpCode.StartCapture, command.OpCode);
        Assert.True(command.CaptureMouse);
        Assert.False(command.CaptureKeyboard);
    }

    private static IpcTransportHandlers CreateNoOpHandlers() => new(
        static (_, _) => { /* No frames are received. */ },
        static _ => Task.CompletedTask,
        static () => { /* Not observed. */ },
        static _ => { /* Not observed. */ },
        static _ => { /* Not observed. */ },
        static (_, _) => { /* Not observed. */ },
        static _ => { /* Not observed. */ });

    private static async Task ReportSendFailureWhileHoldingGateAsync(
        IpcComponentFixture fixture,
        IOException sendFailure,
        Task? pendingCallback)
    {
        var captureGate = fixture.Capture.CommandGate;
        Assert.True(
            await captureGate.WaitAsync(CommandTimeout, CancellationToken.None),
            "Timed out waiting to acquire the capture command gate.");
        try
        {
            var invocationException = Record.Exception(() =>
                fixture.Capture.OnSendFailure(IpcOpCode.StartCapture, sendFailure));

            Assert.Null(invocationException);

            if (pendingCallback is not null)
            {
                Assert.False(
                    pendingCallback.IsCompleted,
                    "Deferred error callbacks should not run before the capture gate is released.");
            }
        }
        finally
        {
            _ = captureGate.Release();
        }
    }
}
