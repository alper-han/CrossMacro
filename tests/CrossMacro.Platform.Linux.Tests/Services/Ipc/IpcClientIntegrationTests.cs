
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

[Collection(nameof(LinuxIpcIntegrationSerialCollection))]
public sealed class IpcClientIntegrationTests
{
    private static readonly TimeSpan AsyncOperationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HandshakeTimeoutAssertionBudget = TimeSpan.FromSeconds(8);

    [LinuxFact]
    public async Task ConnectAsync_WhenDaemonReturnsHandshakeError_ShouldThrowHandshakeFailed()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.ErrorResponse);
        using var client = new IpcClient(() => socketPath);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            client.ConnectAsync(CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.HandshakeFailed, exception.Reason);
    }

    [LinuxFact]
    public async Task ConnectAsync_WhenProtocolVersionMismatches_ShouldThrowProtocolMismatch()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.ProtocolMismatch);
        using var client = new IpcClient(() => socketPath);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            client.ConnectAsync(CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.ProtocolMismatch, exception.Reason);
    }

    [LinuxFact]
    public async Task ConnectAsync_WhenHandshakeTimesOut_ShouldThrowTimeout()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.NoResponse);
        using var client = new IpcClient(() => socketPath);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            client.ConnectAsync(CancellationToken.None).WaitAsync(HandshakeTimeoutAssertionBudget));

        Assert.Equal(IpcClientFailureReason.Timeout, exception.Reason);
    }

    [LinuxFact]
    public async Task ConnectAsync_WhenCallerCancellationFiresDuringHandshake_ShouldPropagateCancellationWithinCallerBudget()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.NoResponse);
        using var client = new IpcClient(() => socketPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var started = DateTime.UtcNow;
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            client.ConnectAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(2)));

        var elapsed = DateTime.UtcNow - started;
        Assert.True(
            elapsed < TimeSpan.FromSeconds(2),
            $"ConnectAsync should honor caller cancellation during handshake. Elapsed: {elapsed}.");
    }

    [LinuxFact]
    public async Task ConnectAsync_WhenCallerTokenIsCancelledAfterHandshake_ShouldKeepReaderAndCaptureAcknowledgements()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var cts = new CancellationTokenSource();

        await client.ConnectAsync(cts.Token);
        cts.Cancel();

        await client.StartCaptureAsync("reader-race", mouse: true, keyboard: false)
            .WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));
        Assert.True(client.IsConnected);
    }

    [LinuxIntegrationFact]
    public async Task WhenConnectionDrops_ShouldAutoReconnectAndReplayActiveCapture()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath);
        var daemon1Disposed = false;
        var client = new IpcClient(() => socketPath);
        var clientDisposed = false;

        try
        {
            await client.ConnectAsync(CancellationToken.None);
            client.StartCapture("global-hotkeys", mouse: true, keyboard: false);
            await daemon1.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            await daemon1.DisposeAsync();
            daemon1Disposed = true;
            await using var daemon2 = await TestIpcDaemon.StartAsync(socketPath);

            await daemon2.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(8));
            var commands = daemon2.GetCommandsSnapshot();

            _ = Assert.Single(commands);
            Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
            Assert.True(commands[0].CaptureMouse);
            Assert.False(commands[0].CaptureKeyboard);

            await client.DisposeAsync();
            clientDisposed = true;
        }
        finally
        {
            if (!clientDisposed)
            {
                await client.DisposeAsync();
            }

            if (!daemon1Disposed)
            {
                await daemon1.DisposeAsync();
            }
        }
    }

    [LinuxIntegrationFact]
    public async Task DisposeAsync_WhenDaemonDropsDuringShutdown_ShouldComplete()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon = await TestIpcDaemon.StartAsync(socketPath);
        var daemonDisposed = false;
        await using var client = new IpcClient(() => socketPath);

        try
        {
            await client.ConnectAsync(CancellationToken.None);
            client.StartCapture("shutdown-race", mouse: true, keyboard: false);
            await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var disposeTask = Task.Run(async () =>
            {
                disposeStarted.SetResult();
                await client.DisposeAsync();
            });

            await disposeStarted.Task.WaitAsync(AsyncOperationTimeout);
            await daemon.DisposeAsync();
            daemonDisposed = true;
            await disposeTask.WaitAsync(AsyncOperationTimeout);
        }
        finally
        {
            if (!daemonDisposed)
            {
                await daemon.DisposeAsync();
            }
        }
    }

    [LinuxIntegrationFact]
    public async Task WhenConnectionDrops_ShouldNotEmitReconnectSuccessViaErrorChannel()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath);
        var daemon1Disposed = false;
        var errors = new ConcurrentQueue<string>();
        var client = new IpcClient(() => socketPath);
        var clientDisposed = false;

        try
        {
            client.ErrorOccurred += (_, args) => errors.Enqueue(args.Message);

            await client.ConnectAsync(CancellationToken.None);
            client.StartCapture("global-hotkeys", mouse: true, keyboard: false);
            await daemon1.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            await daemon1.DisposeAsync();
            daemon1Disposed = true;
            await using var daemon2 = await TestIpcDaemon.StartAsync(socketPath);

            await daemon2.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(8));
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Assert.DoesNotContain(
                errors,
                message => string.Equals(message, "Reconnected to daemon", StringComparison.Ordinal));

            await client.DisposeAsync();
            clientDisposed = true;
        }
        finally
        {
            if (!clientDisposed)
            {
                await client.DisposeAsync();
            }

            if (!daemon1Disposed)
            {
                await daemon1.DisposeAsync();
            }
        }
    }

    [LinuxIntegrationFact]
    public async Task WhenConnectionDrops_WithAutoReconnectDisabled_ShouldNotReplayActiveCapture()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath);
        var daemon1Disposed = false;
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        try
        {
            await client.ConnectAsync(CancellationToken.None);
            client.StartCapture("global-hotkeys", mouse: true, keyboard: false);
            await daemon1.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            await daemon1.DisposeAsync();
            daemon1Disposed = true;
            await using var daemon2 = await TestIpcDaemon.StartAsync(socketPath);

            await Task.Delay(TimeSpan.FromSeconds(2));
            var commands = daemon2.GetCommandsSnapshot();
            Assert.Empty(commands);
        }
        finally
        {
            if (!daemon1Disposed)
            {
                await daemon1.DisposeAsync();
            }
        }
    }

    [LinuxIntegrationFact]
    public async Task DeferredCaptureReconcile_WhenTransportDropsBeforeGateReleases_ShouldCancelWithoutIssuingStaleCommand()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.DelayAllCaptureStartAcks);
        var daemonDisposed = false;
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        try
        {
            await client.ConnectAsync(CancellationToken.None);
            await client.StartCaptureAsync("consumer-a", mouse: false, keyboard: true)
                .WaitAsync(AsyncOperationTimeout);
            await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            client.StartCapture("consumer-b", mouse: true, keyboard: true);
            await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

            var captureGate = client.CaptureCommandGate;

            Assert.True(captureGate.Wait(TimeSpan.FromSeconds(2)), "Timed out waiting to acquire the capture command gate.");
            var stopCaptureTask = Task.Run(() => client.StopCapture("consumer-b"));
            try
            {
                await daemon.DisposeAsync();
                daemonDisposed = true;
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            finally
            {
                _ = captureGate.Release();
            }

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                stopCaptureTask.WaitAsync(AsyncOperationTimeout));
            await Task.Delay(TimeSpan.FromMilliseconds(250));

            var commands = daemon.GetCommandsSnapshot();
            Assert.Equal(2, commands.Length);
            Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
            Assert.Equal(IpcOpCode.StartCapture, commands[1].OpCode);
        }
        finally
        {
            if (!daemonDisposed)
            {
                await daemon.DisposeAsync();
            }
        }
    }

    [LinuxFact]
    public async Task StartStopCapture_MultiConsumer_ShouldSendOnlyAggregateTransitions()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath);

        await client.ConnectAsync(CancellationToken.None);

        client.StartCapture("global-hotkeys", mouse: false, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        client.StartCapture("macro-recorder", mouse: true, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        client.StartCapture("text-expansion", mouse: false, keyboard: true);
        client.StopCapture("text-expansion");
        client.StopCapture("macro-recorder");
        await daemon.WaitForCommandCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(2));

        client.StopCapture("global-hotkeys");

        await daemon.WaitForCommandCountAsync(expected: 4, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(4, commands.Length);

        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.False(commands[0].CaptureMouse);
        Assert.True(commands[0].CaptureKeyboard);

        Assert.Equal(IpcOpCode.StartCapture, commands[1].OpCode);
        Assert.True(commands[1].CaptureMouse);
        Assert.True(commands[1].CaptureKeyboard);

        Assert.Equal(IpcOpCode.StartCapture, commands[2].OpCode);
        Assert.False(commands[2].CaptureMouse);
        Assert.True(commands[2].CaptureKeyboard);

        Assert.Equal(IpcOpCode.StopCapture, commands[3].OpCode);
    }

    [LinuxFact]
    public async Task StartCaptureAsync_WhenPendingStartFailsAfterOtherConsumerUnsubscribes_ShouldRollbackAsyncOriginWithoutRetry()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailSecondStartAfterDelay);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        await client.ConnectAsync(CancellationToken.None);
        await client.StartCaptureAsync("consumer-b", mouse: true, keyboard: false)
            .WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var pendingStartTask = client.StartCaptureAsync("consumer-a", mouse: false, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        client.StopCapture("consumer-b");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pendingStartTask.WaitAsync(AsyncOperationTimeout));
        Assert.Contains("Simulated delayed start failure", exception.Message, StringComparison.Ordinal);

        _ = await Assert.ThrowsAsync<TimeoutException>(() =>
            daemon.WaitForCommandCountAsync(expected: 3, timeout: TimeSpan.FromMilliseconds(500)));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(2, commands.Length);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.Equal(IpcOpCode.StartCapture, commands[1].OpCode);
    }

    [LinuxFact]
    public async Task StartCaptureAsync_WhenWidenFailsAndOtherConsumerUnsubscribes_ShouldReconcileToRemainingMask()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailSecondStartAfterDelay);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        await client.ConnectAsync(CancellationToken.None);

        await client.StartCaptureAsync("consumer-a", mouse: false, keyboard: true)
            .WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        client.StartCapture("consumer-b", mouse: false, keyboard: true);
        _ = Assert.Single(daemon.GetCommandsSnapshot());

        var pendingStartTask = client.StartCaptureAsync("consumer-a", mouse: true, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        client.StopCapture("consumer-b");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pendingStartTask.WaitAsync(AsyncOperationTimeout));
        Assert.Contains("Simulated delayed start failure", exception.Message, StringComparison.Ordinal);

        await daemon.WaitForCommandCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(3, commands.Length);

        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.False(commands[0].CaptureMouse);
        Assert.True(commands[0].CaptureKeyboard);

        Assert.Equal(IpcOpCode.StartCapture, commands[1].OpCode);
        Assert.True(commands[1].CaptureMouse);
        Assert.True(commands[1].CaptureKeyboard);

        Assert.Equal(IpcOpCode.StartCapture, commands[2].OpCode);
        Assert.False(commands[2].CaptureMouse);
        Assert.True(commands[2].CaptureKeyboard);
    }

    [LinuxFact]
    public async Task StartCaptureAsync_WhenSharedStartFailsButSyncConsumerStillNeedsMask_ShouldRetrySameMask()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailSecondStartAfterDelay);
        var daemonDisposed = false;
        var client = new IpcClient(() => socketPath, autoReconnect: false);
        var clientDisposed = false;

        try
        {
            await client.ConnectAsync(CancellationToken.None);

            await client.StartCaptureAsync("consumer-a", mouse: false, keyboard: true)
                .WaitAsync(AsyncOperationTimeout);
            await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

            var pendingStartTask = client.StartCaptureAsync("consumer-a", mouse: true, keyboard: true);
            await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

            client.StartCapture("consumer-sync", mouse: true, keyboard: true);
            Assert.Equal(2, daemon.GetCommandsSnapshot().Length);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pendingStartTask.WaitAsync(AsyncOperationTimeout));
            Assert.Contains("Simulated delayed start failure", exception.Message, StringComparison.Ordinal);

            await daemon.WaitForCommandCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(2));
            var commands = daemon.GetCommandsSnapshot();

            Assert.Equal(IpcOpCode.StartCapture, commands[2].OpCode);
            Assert.True(commands[2].CaptureMouse);
            Assert.True(commands[2].CaptureKeyboard);

            await client.DisposeAsync();
            clientDisposed = true;

            await daemon.DisposeAsync();
            daemonDisposed = true;
        }
        finally
        {
            if (!clientDisposed)
            {
                await client.DisposeAsync();
            }

            if (!daemonDisposed)
            {
                await daemon.DisposeAsync();
            }
        }
    }

    [LinuxFact]
    public async Task StartCapture_WhenSyncJoinRequestsFailureNotification_ShouldObserveErrorBeforePendingTaskCompletion()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailFirstStartAfterDelay);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        await client.ConnectAsync(CancellationToken.None);
        var pendingStartTask = client.StartCaptureAsync("consumer-a", mouse: true, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var errorObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionObservedBeforeError = 0;
        client.ErrorOccurred += (_, _) =>
        {
            if (pendingStartTask.IsCompleted)
            {
                _ = Interlocked.Exchange(ref completionObservedBeforeError, 1);
            }

            _ = errorObserved.TrySetResult();
        };

        client.StartCapture("consumer-sync", mouse: true, keyboard: true);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pendingStartTask.WaitAsync(AsyncOperationTimeout));
        await errorObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, Volatile.Read(ref completionObservedBeforeError));
    }

    [LinuxFact]
    public async Task StartCaptureAsync_WhenCallsOverlap_ShouldReuseSingleInFlightStart()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.DelayAllCaptureStartAcks);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        await client.ConnectAsync(CancellationToken.None);

        var firstStart = client.StartCaptureAsync("shared-consumer", mouse: true, keyboard: true);
        var secondStart = client.StartCaptureAsync("shared-consumer", mouse: true, keyboard: true);

        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));
        _ = Assert.Single(daemon.GetCommandsSnapshot());

        await Task.WhenAll(firstStart, secondStart).WaitAsync(AsyncOperationTimeout);

        var commands = daemon.GetCommandsSnapshot();
        _ = Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.True(commands[0].CaptureKeyboard);
    }

    [LinuxFact]
    public async Task StartCaptureAsync_WhenSameConsumerOverlapsAndSharedStartFails_ShouldRollbackAndReconcilePreviousMask()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailSecondStartAfterDelay);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        await client.ConnectAsync(CancellationToken.None);

        await client.StartCaptureAsync("shared-consumer", mouse: false, keyboard: true)
            .WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var firstStart = client.StartCaptureAsync("shared-consumer", mouse: true, keyboard: true);
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        var secondStart = client.StartCaptureAsync("shared-consumer", mouse: true, keyboard: true);

        var firstException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            firstStart.WaitAsync(AsyncOperationTimeout));
        Assert.Contains("Simulated delayed start failure", firstException.Message, StringComparison.Ordinal);

        var secondException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            secondStart.WaitAsync(AsyncOperationTimeout));
        Assert.Contains("Simulated delayed start failure", secondException.Message, StringComparison.Ordinal);

        await daemon.WaitForCommandCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(2));
        var commands = daemon.GetCommandsSnapshot();

        Assert.Equal(3, commands.Length);
        Assert.Equal(IpcOpCode.StartCapture, commands[2].OpCode);
        Assert.False(commands[2].CaptureMouse);
        Assert.True(commands[2].CaptureKeyboard);
    }

    [LinuxFact]
    public async Task ConnectAsync_AfterInitialFailure_ShouldReplayPendingCaptureSubscription()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath);

        client.StartCapture("global-hotkeys", mouse: true, keyboard: false);

        var exception = await Assert.ThrowsAsync<IpcClientException>(() =>
            client.ConnectAsync(CancellationToken.None));
        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);

        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);

        await client.ConnectAsync(CancellationToken.None);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        _ = Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.False(commands[0].CaptureKeyboard);
    }

    [LinuxFact]
    public async Task ConnectAsync_WhenReplayFindsPendingCaptureStart_ShouldReissueAndCompleteOriginalWaiter()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        client.StartCapture("global-hotkeys", mouse: true, keyboard: false);
        var stalePending = CreatePendingCaptureStart(
            client,
            new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true, CaptureKeyboard: false));

        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);

        await client.ConnectAsync(CancellationToken.None).WaitAsync(AsyncOperationTimeout);

        _ = await stalePending.Completion.Task.WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        _ = Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.False(commands[0].CaptureKeyboard);
        Assert.NotEqual(stalePending.RequestId, commands[0].RequestId);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenHandshakeTimesOut_ShouldRaiseFriendlyError()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.NoResponse);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, "test-capture");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None).WaitAsync(HandshakeTimeoutAssertionBudget));

        Assert.Contains("Timed out while waiting for daemon handshake", exception.Message, StringComparison.Ordinal);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenSocketIsMissing_ShouldRaiseFriendlySocketError()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, "missing-socket-capture");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None).WaitAsync(AsyncOperationTimeout));

        Assert.Contains("Failed to connect to daemon.", exception.Message, StringComparison.Ordinal);
        _ = Assert.IsType<IpcClientException>(exception.InnerException);
        Assert.Equal(IpcClientFailureReason.ConnectFailed, ((IpcClientException)exception.InnerException!).Reason);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenProtocolVersionMismatches_ShouldRaiseFriendlyMismatchError()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.ProtocolMismatch);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, "protocol-mismatch-capture");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None).WaitAsync(AsyncOperationTimeout));

        Assert.Contains("Protocol version mismatch.", exception.Message, StringComparison.Ordinal);
        _ = Assert.IsType<IpcClientException>(exception.InnerException);
        Assert.Equal(IpcClientFailureReason.ProtocolMismatch, ((IpcClientException)exception.InnerException!).Reason);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenInitialProtocolMismatchWithAutoReconnectEnabled_ShouldFailImmediately()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.ProtocolMismatch);
        using var client = new IpcClient(() => socketPath, autoReconnect: true);
        using var capture = new LinuxIpcInputCapture(client, "protocol-mismatch-autoreconnect-capture");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None).WaitAsync(AsyncOperationTimeout));

        Assert.Contains("Protocol version mismatch.", exception.Message, StringComparison.Ordinal);
        _ = Assert.IsType<IpcClientException>(exception.InnerException);
        Assert.Equal(IpcClientFailureReason.ProtocolMismatch, ((IpcClientException)exception.InnerException!).Reason);
    }

    [LinuxIntegrationFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenStartupTransportDrops_ShouldWaitForReconnectAndSucceed()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.DelayAllCaptureStartAcks);
        var daemon1Disposed = false;
        using var client = new IpcClient(() => socketPath, autoReconnect: true);
        await client.ConnectAsync(CancellationToken.None);
        using var capture = new LinuxIpcInputCapture(client, "reconnect-startup-capture");
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var startTask = capture.StartAsync(CancellationToken.None);
        await daemon1.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        await daemon1.DisposeAsync();
        daemon1Disposed = true;
        await using var daemon2 = await TestIpcDaemon.StartAsync(socketPath);

        await startTask.WaitAsync(TimeSpan.FromSeconds(8));
        await daemon2.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon2.GetCommandsSnapshot();
        _ = Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.False(commands[0].CaptureKeyboard);

        if (!daemon1Disposed)
        {
            await daemon1.DisposeAsync();
        }
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenConnected_ShouldSendStartAndStop()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        using var capture = new LinuxIpcInputCapture(client, "integration-capture");
        capture.Configure(captureMouse: true, captureKeyboard: false);
        using var cts = new CancellationTokenSource();

        await capture.StartAsync(cts.Token).WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        cts.Cancel();
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.False(commands[0].CaptureKeyboard);
        Assert.Equal(IpcOpCode.StopCapture, commands[1].OpCode);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_DisposeAsync_WhenStarted_ShouldSendStopCapture()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        var capture = new LinuxIpcInputCapture(client, "async-dispose-capture");
        capture.Configure(captureMouse: true, captureKeyboard: true);

        await capture.StartAsync(CancellationToken.None).WaitAsync(AsyncOperationTimeout);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        await capture.DisposeAsync();
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.Equal(IpcOpCode.StopCapture, commands[1].OpCode);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenCallsOverlapOnSameInstance_ShouldReuseInFlightStartup()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.DelayAllCaptureStartAcks);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        using var capture = new LinuxIpcInputCapture(client, "integration-capture-overlap");
        capture.Configure(captureMouse: true, captureKeyboard: true);

        var firstStart = capture.StartAsync(CancellationToken.None);
        var secondStart = capture.StartAsync(CancellationToken.None);

        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));
        _ = Assert.Single(daemon.GetCommandsSnapshot());

        await Task.WhenAll(firstStart, secondStart).WaitAsync(AsyncOperationTimeout);

        var commands = daemon.GetCommandsSnapshot();
        _ = Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_StartAsync_WhenTokenAlreadyCancelled_ShouldPropagateCancellation()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, "cancelled-capture");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            capture.StartAsync(cts.Token).WaitAsync(AsyncOperationTimeout));
    }

    [LinuxFact]
    public async Task LinuxIpcInputCapture_Dispose_WhenStartupIsInFlight_ShouldCancelAndStopCapture()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.DelayAllCaptureStartAcks);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        var capture = new LinuxIpcInputCapture(client, "dispose-during-start-capture");
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var startTask = capture.StartAsync(CancellationToken.None);
        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));

        capture.Dispose();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => startTask.WaitAsync(AsyncOperationTimeout));
        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        var commands = daemon.GetCommandsSnapshot();
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.Equal(IpcOpCode.StopCapture, commands[1].OpCode);
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_InitializeAsync_WhenConnectionFails_ShouldNotThrow()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client);

        await simulator.InitializeAsync(screenWidth: 1920, screenHeight: 1080, CancellationToken.None);
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_InitializeAsync_WhenCallerCancellationFires_ShouldPropagateCancellation()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            simulator.InitializeAsync(cancellationToken: cts.Token).WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [LinuxFact]
    public async Task IpcClient_SyncBatchAndAsyncCommandShareOneWriteGate()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        var writeGate = client.WriteGate;
        Assert.True(await writeGate.WaitAsync(TimeSpan.FromSeconds(1)));
        try
        {
            InputSimulationStep[] steps = [new(0x01, 30, 1)];
            var batchTask = Task.Run(() => client.SimulateEventBatch(steps));
            var commandTask = client.StartCaptureAsync("write-gate", mouse: true, keyboard: false);

            await Task.Delay(100);
            Assert.Empty(daemon.GetCommandsSnapshot());
            _ = writeGate.Release();

            await Task.WhenAll(batchTask, commandTask).WaitAsync(AsyncOperationTimeout);
        }
        finally
        {
            if (writeGate.CurrentCount is 0)
            {
                _ = writeGate.Release();
            }
        }

        await daemon.WaitForCommandCountAsync(expected: 2, timeout: TimeSpan.FromSeconds(2));

        // SemaphoreSlim gives no FIFO guarantee, so assert the set of commands, not their order.
        var opCodes = daemon.GetCommandsSnapshot().Select(static command => command.OpCode).ToArray();
        Assert.Equal(2, opCodes.Length);
        Assert.Contains(IpcOpCode.SimulateEventBatch, opCodes);
        Assert.Contains(IpcOpCode.StartCapture, opCodes);
    }

    [LinuxFact]
    public async Task IpcClient_AsyncCommandCanceledWhileWaitingForWriteGate_DoesNotSendAndReleasesCaptureState()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        var writeGate = client.WriteGate;
        Assert.True(writeGate.Wait(TimeSpan.FromSeconds(1)));
        try
        {
            using var cancellation = new CancellationTokenSource();
            var canceledStart = client.StartCaptureAsync(
                "canceled-write-gate",
                mouse: true,
                keyboard: false,
                cancellation.Token);

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            await cancellation.CancelAsync();
            await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() =>
                canceledStart.WaitAsync(AsyncOperationTimeout));

            Assert.Empty(daemon.GetCommandsSnapshot());
            _ = writeGate.Release();

            await client.StartCaptureAsync("successful-write-gate", mouse: true, keyboard: false)
                .WaitAsync(AsyncOperationTimeout);
        }
        finally
        {
            if (writeGate.CurrentCount is 0)
            {
                _ = writeGate.Release();
            }
        }

        await daemon.WaitForCommandCountAsync(expected: 1, timeout: TimeSpan.FromSeconds(2));
        var command = Assert.Single(daemon.GetCommandsSnapshot());
        Assert.Equal(IpcOpCode.StartCapture, command.OpCode);
        Assert.True(command.CaptureMouse);
        Assert.False(command.CaptureKeyboard);
    }

    [LinuxFact]
    public void SimulateEvent_WhenDisconnected_ShouldThrowConnectFailed()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        var exception = Assert.Throws<IpcClientException>(() =>
            client.SimulateEvent(type: 1, code: 2, value: 3));

        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);
    }

    [LinuxFact]
    public void SimulateEvents_WhenDisconnected_ShouldThrowConnectFailed()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        var exception = Assert.Throws<IpcClientException>(() =>
        {
            (ushort Type, ushort Code, int Value)[] events = [(1, 2, 3)];
            client.SimulateEvents(events);
        });

        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);
    }

    [LinuxFact]
    public void SimulateEventBatch_WhenDisconnected_ShouldThrowConnectFailed()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

        var exception = Assert.Throws<IpcClientException>(() =>
        {
            InputSimulationStep[] events = [new(1, 2, 3)];
            client.SimulateEventBatch(events);
        });

        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_WhenConnected_ShouldSendConfigureAndSimulateEvents()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);
        using var simulator = new LinuxIpcInputSimulator(client);

        await simulator.InitializeAsync(screenWidth: 1920, screenHeight: 1080, CancellationToken.None);
        simulator.MoveRelative(dx: 5, dy: -3);
        simulator.MouseButton(button: 1, pressed: true);
        simulator.Scroll(delta: -2, isHorizontal: true);
        simulator.KeyPress(keyCode: 30, pressed: true);
        simulator.Sync();

        await daemon.WaitForCommandCountAsync(expected: 11, timeout: TimeSpan.FromSeconds(2));
        var commands = daemon.GetCommandsSnapshot();

        Assert.Contains(commands, c => c.OpCode is IpcOpCode.ConfigureResolution && c.Width is 1920 && c.Height is 1080);
        Assert.DoesNotContain(commands, c => c.OpCode is IpcOpCode.SimulateEvent);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x02 && c.Code is 0x00 && c.Value is 5);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x02 && c.Code is 0x01 && c.Value == -3);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x01 && c.Code is 1 && c.Value is 1);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x02 && c.Code is 0x06 && c.Value == -2);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x01 && c.Code is 30 && c.Value is 1);
        Assert.Contains(commands, c => c.OpCode is IpcOpCode.SimulateEventBatch && c.Type is 0x00 && c.Code is 0x00 && c.Value is 0);
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_WhenBatchSupported_ShouldSendBatchAndWaitForAck()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);
        using var simulator = new LinuxIpcInputSimulator(client);

        Assert.True(simulator.SupportsBatchedInput);

        InputSimulationStep[] steps =
        [
            new(0x01, 30, 1, 1),
            new(0x00, 0, 0, 2),
            new(0x01, 30, 0, 3),
            new(0x00, 0, 0, 4),
        ];

        await simulator.SimulateBatchAsync(steps, CancellationToken.None);

        await daemon.WaitForCommandCountAsync(expected: 4, timeout: TimeSpan.FromSeconds(2));
        var commands = daemon.GetCommandsSnapshot();

        Assert.All(commands, command => Assert.Equal(IpcOpCode.SimulateEventBatch, command.OpCode));
        Assert.Equal((0x01, 30, 1, 1L), (commands[0].Type, commands[0].Code, commands[0].Value, commands[0].DelayAfterMicroseconds));
        Assert.Equal((0x00, 0, 0, 2L), (commands[1].Type, commands[1].Code, commands[1].Value, commands[1].DelayAfterMicroseconds));
        Assert.Equal((0x01, 30, 0, 3L), (commands[2].Type, commands[2].Code, commands[2].Value, commands[2].DelayAfterMicroseconds));
        Assert.Equal((0x00, 0, 0, 4L), (commands[3].Type, commands[3].Code, commands[3].Value, commands[3].DelayAfterMicroseconds));
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_MoveAbsolute_ShouldWaitForDaemonBatchAcknowledgement()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);
        using var simulator = new LinuxIpcInputSimulator(client);

        await simulator.InitializeAsync(screenWidth: 5120, screenHeight: 1440, CancellationToken.None);
        simulator.MoveAbsolute(1775, 153);

        await daemon.WaitForCommandCountAsync(expected: 4, timeout: TimeSpan.FromSeconds(2));
        var commands = daemon.GetCommandsSnapshot();
        var batchCommands = commands.Where(command => command.OpCode is IpcOpCode.SimulateEventBatch).ToArray();

        Assert.Equal(3, batchCommands.Length);
        Assert.Equal((0x03, 0, 1775, 0L), (batchCommands[0].Type, batchCommands[0].Code, batchCommands[0].Value, batchCommands[0].DelayAfterMicroseconds));
        Assert.Equal((0x03, 1, 153, 0L), (batchCommands[1].Type, batchCommands[1].Code, batchCommands[1].Value, batchCommands[1].DelayAfterMicroseconds));
        Assert.Equal((0x00, 0, 0, 0L), (batchCommands[2].Type, batchCommands[2].Code, batchCommands[2].Value, batchCommands[2].DelayAfterMicroseconds));
    }

    [LinuxFact]
    public async Task LinuxIpcInputSimulator_AbsoluteTrajectory_SendsAbsoluteSamplesAndMicrosecondDelaysInOneAcknowledgedBatch()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);
        using var simulator = new LinuxIpcInputSimulator(client);
        await simulator.InitializeAsync(screenWidth: 5120, screenHeight: 1440, CancellationToken.None);

        await simulator.SimulateAbsoluteTrajectoryAsync(
        [
            new AbsoluteMotionTrajectorySample(1775, 153, 2_375),
            new AbsoluteMotionTrajectorySample(1788, 160, 250),
        ],
        CancellationToken.None);

        await daemon.WaitForCommandCountAsync(expected: 7, timeout: TimeSpan.FromSeconds(2));
        var batchCommands = daemon.GetCommandsSnapshot()
            .Where(command => command.OpCode is IpcOpCode.SimulateEventBatch)
            .ToArray();

        Assert.Equal(6, batchCommands.Length);
        Assert.Equal((0x03, 0, 1775, 0L), (batchCommands[0].Type, batchCommands[0].Code, batchCommands[0].Value, batchCommands[0].DelayAfterMicroseconds));
        Assert.Equal((0x03, 1, 153, 0L), (batchCommands[1].Type, batchCommands[1].Code, batchCommands[1].Value, batchCommands[1].DelayAfterMicroseconds));
        Assert.Equal((0x00, 0, 0, 2_375L), (batchCommands[2].Type, batchCommands[2].Code, batchCommands[2].Value, batchCommands[2].DelayAfterMicroseconds));
        Assert.Equal((0x03, 0, 1788, 0L), (batchCommands[3].Type, batchCommands[3].Code, batchCommands[3].Value, batchCommands[3].DelayAfterMicroseconds));
        Assert.Equal((0x03, 1, 160, 0L), (batchCommands[4].Type, batchCommands[4].Code, batchCommands[4].Value, batchCommands[4].DelayAfterMicroseconds));
        Assert.Equal((0x00, 0, 0, 250L), (batchCommands[5].Type, batchCommands[5].Code, batchCommands[5].Value, batchCommands[5].DelayAfterMicroseconds));
    }

    [LinuxFact]
    public async Task SimulateEventBatch_WhenDaemonReturnsFailure_ShouldThrowSimulationRejected()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.FailSimulationBatch);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        InputSimulationStep[] steps = [new(0x01, 30, 1)];

        var exception = Assert.Throws<IpcClientException>(() => client.SimulateEventBatch(steps));

        Assert.Equal(IpcClientFailureReason.SimulationRejected, exception.Reason);
        Assert.Contains("Simulation batch failed", exception.Message, StringComparison.Ordinal);
    }

    [LinuxFact]
    public async Task SimulateEventBatch_WhenDaemonAcknowledgementCountDiffers_ShouldThrowIntegrityMismatch()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.CorruptSimulationBatchCount);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        InputSimulationStep[] steps = [new(0x01, 30, 1, 2_500)];

        var exception = Assert.Throws<IpcClientException>(() => client.SimulateEventBatch(steps));

        Assert.Equal(IpcClientFailureReason.IntegrityMismatch, exception.Reason);
        Assert.Contains("event-count mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public async Task SimulateEventBatch_WhenTransportDropsBeforeAck_ShouldFailPendingWaiter()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.DropSimulationBatchBeforeAck);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        InputSimulationStep[] steps = [new(0x01, 30, 1)];

        var batchTask = Task.Run(() => client.SimulateEventBatch(steps));
        var exception = await Assert.ThrowsAsync<IpcClientException>(() => batchTask.WaitAsync(AsyncOperationTimeout));

        Assert.Equal(IpcClientFailureReason.ConnectFailed, exception.Reason);
    }

    [LinuxFact]
    public async Task SimulateEventBatch_WhenDaemonKeepsConnectionOpenWithoutAck_ShouldTimeout()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath, HandshakeBehavior.HoldSimulationBatchWithoutAck);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);

        InputSimulationStep[] steps = [new(0x01, 30, 1)];

        var batchTask = Task.Run(() => client.SimulateEventBatch(steps));
        var exception = await Assert.ThrowsAsync<IpcClientException>(() => batchTask.WaitAsync(TimeSpan.FromSeconds(8)));

        Assert.Equal(IpcClientFailureReason.Timeout, exception.Reason);
    }

    private static string GetUniqueSocketPath() =>
        TestSocketPaths.CreateShort("cm-ipc");

    private static PendingCaptureStartRegistration CreatePendingCaptureStart(
        IpcClient client,
        CaptureCommand command)
    {
        return client.PendingCaptureStarts.Begin(command, notifyOnFailure: true);
    }

    private enum HandshakeBehavior
    {
        Success = 0,
        ErrorResponse = 1,
        ProtocolMismatch = 2,
        NoResponse = 3,
        FailSecondStartAfterDelay = 4,
        DelayAllCaptureStartAcks = 5,
        FailFirstStartAfterDelay = 6,
        FailSimulationBatch = 7,
        DropSimulationBatchBeforeAck = 8,
        HoldSimulationBatchWithoutAck = 9,
        CorruptSimulationBatchCount = 10,
    }

    private readonly record struct CapturedCommand(
        IpcOpCode OpCode,
        int RequestId = 0,
        bool CaptureMouse = false,
        bool CaptureKeyboard = false,
        ushort Type = 0,
        ushort Code = 0,
        int Value = 0,
        long DelayAfterMicroseconds = 0,
        int Width = 0,
        int Height = 0);

    private sealed class TestIpcDaemon : IAsyncDisposable
    {
        private readonly string _socketPath;
        private readonly HandshakeBehavior _handshakeBehavior;
        private readonly Socket _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<CapturedCommand> _commands = new();
        private readonly SemaphoreSlim _commandSignal = new(0);
        private Task? _serverTask;
        private Socket? _clientSocket;
        private int _startCaptureCount;

        private TestIpcDaemon(string socketPath, HandshakeBehavior handshakeBehavior)
        {
            _socketPath = socketPath;
            _handshakeBehavior = handshakeBehavior;
            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        }

        public static async Task<TestIpcDaemon> StartAsync(
            string socketPath,
            HandshakeBehavior handshakeBehavior = HandshakeBehavior.Success)
        {
            var daemon = new TestIpcDaemon(socketPath, handshakeBehavior);
            await daemon.StartInternalAsync();
            return daemon;
        }

        public CapturedCommand[] GetCommandsSnapshot() => _commands.ToArray();

        public async Task WaitForCommandCountAsync(int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_commands.Count < expected)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException($"Timed out waiting for {expected} IPC command(s). Received {_commands.Count}.");
                }

                _ = await _commandSignal.WaitAsync(remaining);
            }
        }

        private async Task StartInternalAsync()
        {
            var dir = Path.GetDirectoryName(_socketPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }

            _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            _listener.Listen(1);
            _serverTask = RunServerAsync(_cts.Token);

            await Task.Yield();
        }

        private async Task RunServerAsync(CancellationToken token)
        {
            try
            {
                _clientSocket = await _listener.AcceptAsync(token);
                using var stream = new NetworkStream(_clientSocket, ownsSocket: false);
                using var reader = new BinaryReader(stream);
                using var writer = new BinaryWriter(stream);

                var handshakeOp = (IpcOpCode)reader.ReadByte();
                var protocolVersion = reader.ReadInt32();
                if (handshakeOp is not IpcOpCode.Handshake || protocolVersion != IpcProtocol.ProtocolVersion)
                {
                    writer.Write((byte)IpcOpCode.Error);
                    writer.Write("Invalid handshake");
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior is HandshakeBehavior.ErrorResponse)
                {
                    writer.Write((byte)IpcOpCode.Error);
                    writer.Write("Authorization denied");
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior is HandshakeBehavior.ProtocolMismatch)
                {
                    writer.Write((byte)IpcOpCode.Handshake);
                    writer.Write(IpcProtocol.ProtocolVersion + 1);
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior is HandshakeBehavior.NoResponse)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return;
                }

                writer.Write((byte)IpcOpCode.Handshake);
                writer.Write(IpcProtocol.ProtocolVersion);
                stream.Flush();

                while (!token.IsCancellationRequested)
                {
                    IpcOpCode opCode;
                    try
                    {
                        opCode = (IpcOpCode)reader.ReadByte();
                    }
                    catch (IOException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    switch (opCode)
                    {
                        case IpcOpCode.StartCapture:
                            var requestId = reader.ReadInt32();
                            var captureMouse = reader.ReadBoolean();
                            var captureKeyboard = reader.ReadBoolean();
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                RequestId: requestId,
                                CaptureMouse: captureMouse,
                                CaptureKeyboard: captureKeyboard));
                            _ = _commandSignal.Release();
                            _startCaptureCount++;
                            if (_handshakeBehavior is HandshakeBehavior.FailFirstStartAfterDelay && _startCaptureCount is 1)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
                                if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                                {
                                    return;
                                }
                                break;
                            }

                            if (_handshakeBehavior is HandshakeBehavior.FailSecondStartAfterDelay && _startCaptureCount is 2)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
                                if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                                {
                                    return;
                                }
                                break;
                            }

                            if (_handshakeBehavior is HandshakeBehavior.DelayAllCaptureStartAcks)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(200), token);
                            }

                            if (!TryWriteCaptureStarted(writer, stream, requestId))
                            {
                                return;
                            }
                            break;
                        case IpcOpCode.StopCapture:
                            _commands.Enqueue(new CapturedCommand(OpCode: opCode));
                            _ = _commandSignal.Release();
                            break;
                        case IpcOpCode.SimulateEvent:
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                Type: reader.ReadUInt16(),
                                Code: reader.ReadUInt16(),
                                Value: reader.ReadInt32()));
                            _ = _commandSignal.Release();
                            break;
                        case IpcOpCode.SimulateEventBatch:
                            var simulationRequestId = reader.ReadInt32();
                            var eventCount = reader.ReadInt32();
                            for (var i = 0; i < eventCount; i++)
                            {
                                var type = reader.ReadUInt16();
                                var code = reader.ReadUInt16();
                                var value = reader.ReadInt32();
                                var delayAfterMicroseconds = reader.ReadInt64();
                                _commands.Enqueue(new CapturedCommand(
                                    OpCode: opCode,
                                    RequestId: simulationRequestId,
                                    Type: type,
                                    Code: code,
                                    Value: value,
                                    DelayAfterMicroseconds: delayAfterMicroseconds));
                                _ = _commandSignal.Release();
                            }

                            if (_handshakeBehavior is HandshakeBehavior.DropSimulationBatchBeforeAck)
                            {
                                _clientSocket?.Dispose();
                                return;
                            }

                            if (_handshakeBehavior is HandshakeBehavior.HoldSimulationBatchWithoutAck)
                            {
                                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                                return;
                            }

                            if (_handshakeBehavior is HandshakeBehavior.FailSimulationBatch)
                            {
                                if (!TryWriteSimulationBatchFailed(writer, stream, simulationRequestId, "Simulated batch failure"))
                                {
                                    return;
                                }

                                break;
                            }

                            var acknowledgedEventCount = _handshakeBehavior is HandshakeBehavior.CorruptSimulationBatchCount
                                ? eventCount + 1
                                : eventCount;

                            if (!TryWriteSimulationBatchCompleted(
                                    writer,
                                    stream,
                                    simulationRequestId,
                                    acknowledgedEventCount))
                            {
                                return;
                            }

                            break;
                        case IpcOpCode.ConfigureResolution:
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                Width: reader.ReadInt32(),
                                Height: reader.ReadInt32()));
                            _ = _commandSignal.Release();
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private static bool TryWriteCaptureStarted(BinaryWriter writer, Stream stream, int requestId)
        {
            try
            {
                writer.Write((byte)IpcOpCode.CaptureStarted);
                writer.Write(requestId);
                stream.Flush();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool TryWriteCaptureStartFailed(BinaryWriter writer, Stream stream, int requestId, string message)
        {
            try
            {
                writer.Write((byte)IpcOpCode.CaptureStartFailed);
                writer.Write(requestId);
                writer.Write(message);
                stream.Flush();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool TryWriteSimulationBatchCompleted(
            BinaryWriter writer,
            Stream stream,
            int requestId,
            int eventCount)
        {
            try
            {
                writer.Write((byte)IpcOpCode.SimulationBatchCompleted);
                writer.Write(requestId);
                writer.Write(eventCount);
                stream.Flush();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool TryWriteSimulationBatchFailed(BinaryWriter writer, Stream stream, int requestId, string message)
        {
            try
            {
                writer.Write((byte)IpcOpCode.SimulationBatchFailed);
                writer.Write(requestId);
                writer.Write(message);
                stream.Flush();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();

            if (_clientSocket is not null)
            {
                try
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    _clientSocket.Close(0);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            try
            {
                _listener.Close(0);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            if (_serverTask is not null)
            {
                try
                {
                    await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException)
                {
                }
                catch (TimeoutException)
                {
                }
            }

            _cts.Dispose();
            _commandSignal.Dispose();

            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
    }
}
