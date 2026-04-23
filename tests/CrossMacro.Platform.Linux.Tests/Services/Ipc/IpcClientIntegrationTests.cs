using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Ipc;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

[CollectionDefinition(nameof(LinuxIpcIntegrationSerialCollection), DisableParallelization = true)]
public sealed class LinuxIpcIntegrationSerialCollection;

[Collection(nameof(LinuxIpcIntegrationSerialCollection))]
public class IpcClientIntegrationTests
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
    public async Task WhenConnectionDrops_ShouldAutoReconnectAndReplayActiveCapture()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath);
        var daemon1Disposed = false;
        using var client = new IpcClient(() => socketPath);

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

            Assert.Single(commands);
            Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
            Assert.True(commands[0].CaptureMouse);
            Assert.False(commands[0].CaptureKeyboard);
        }
        finally
        {
            if (!daemon1Disposed)
            {
                await daemon1.DisposeAsync();
            }
        }
    }

    [LinuxFact]
    public async Task WhenConnectionDrops_ShouldNotEmitReconnectSuccessViaErrorChannel()
    {
        var socketPath = GetUniqueSocketPath();
        var daemon1 = await TestIpcDaemon.StartAsync(socketPath);
        var daemon1Disposed = false;
        var errors = new ConcurrentQueue<string>();
        using var client = new IpcClient(() => socketPath);

        try
        {
            client.ErrorOccurred += (_, message) => errors.Enqueue(message);

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
        }
        finally
        {
            if (!daemon1Disposed)
            {
                await daemon1.DisposeAsync();
            }
        }
    }

    [LinuxFact]
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

        await Task.Delay(TimeSpan.FromMilliseconds(350));

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
        Assert.Single(daemon.GetCommandsSnapshot());

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
        await using var daemon = await TestIpcDaemon.StartAsync(
            socketPath,
            HandshakeBehavior.FailSecondStartAfterDelay);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);

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
                Interlocked.Exchange(ref completionObservedBeforeError, 1);
            }

            errorObserved.TrySetResult();
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
        Assert.Single(daemon.GetCommandsSnapshot());

        await Task.WhenAll(firstStart, secondStart).WaitAsync(AsyncOperationTimeout);

        var commands = daemon.GetCommandsSnapshot();
        Assert.Single(commands);
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
        Assert.Single(commands);
        Assert.Equal(IpcOpCode.StartCapture, commands[0].OpCode);
        Assert.True(commands[0].CaptureMouse);
        Assert.False(commands[0].CaptureKeyboard);
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
        Assert.Single(daemon.GetCommandsSnapshot());

        await Task.WhenAll(firstStart, secondStart).WaitAsync(AsyncOperationTimeout);

        var commands = daemon.GetCommandsSnapshot();
        Assert.Single(commands);
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

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            capture.StartAsync(cts.Token).WaitAsync(AsyncOperationTimeout));
    }

    [LinuxFact]
    public void LinuxIpcInputSimulator_Initialize_WhenConnectionFails_ShouldNotThrow()
    {
        var socketPath = GetUniqueSocketPath();
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client);

        var exception = Record.Exception(() => simulator.Initialize(screenWidth: 1920, screenHeight: 1080));
        Assert.Null(exception);
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
    public async Task LinuxIpcInputSimulator_WhenConnected_ShouldSendConfigureAndSimulateEvents()
    {
        var socketPath = GetUniqueSocketPath();
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        await client.ConnectAsync(CancellationToken.None);
        using var simulator = new LinuxIpcInputSimulator(client);

        simulator.Initialize(screenWidth: 1920, screenHeight: 1080);
        simulator.MoveRelative(dx: 5, dy: -3);
        simulator.MouseButton(button: 1, pressed: true);
        simulator.Scroll(delta: -2, isHorizontal: true);
        simulator.KeyPress(keyCode: 30, pressed: true);
        simulator.Sync();

        await daemon.WaitForCommandCountAsync(expected: 11, timeout: TimeSpan.FromSeconds(2));
        var commands = daemon.GetCommandsSnapshot();

        Assert.Contains(commands, c => c.OpCode == IpcOpCode.ConfigureResolution && c.Width == 1920 && c.Height == 1080);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x02 && c.Code == 0x00 && c.Value == 5);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x02 && c.Code == 0x01 && c.Value == -3);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x01 && c.Code == 1 && c.Value == 1);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x02 && c.Code == 0x06 && c.Value == -2);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x01 && c.Code == 30 && c.Value == 1);
        Assert.Contains(commands, c => c.OpCode == IpcOpCode.SimulateEvent && c.Type == 0x00 && c.Code == 0x00 && c.Value == 0);
    }

    private static string GetUniqueSocketPath() =>
        Path.Combine(Path.GetTempPath(), $"crossmacro-ipc-test-{Guid.NewGuid():N}.sock");

    private enum HandshakeBehavior
    {
        Success = 0,
        ErrorResponse = 1,
        ProtocolMismatch = 2,
        NoResponse = 3,
        FailSecondStartAfterDelay = 4,
        DelayAllCaptureStartAcks = 5,
        FailFirstStartAfterDelay = 6
    }

    private readonly record struct CapturedCommand(
        IpcOpCode OpCode,
        int RequestId = 0,
        bool CaptureMouse = false,
        bool CaptureKeyboard = false,
        ushort Type = 0,
        ushort Code = 0,
        int Value = 0,
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

                await _commandSignal.WaitAsync(remaining);
            }
        }

        private async Task StartInternalAsync()
        {
            var dir = Path.GetDirectoryName(_socketPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
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
                if (handshakeOp != IpcOpCode.Handshake || protocolVersion != IpcProtocol.ProtocolVersion)
                {
                    writer.Write((byte)IpcOpCode.Error);
                    writer.Write("Invalid handshake");
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior == HandshakeBehavior.ErrorResponse)
                {
                    writer.Write((byte)IpcOpCode.Error);
                    writer.Write("Authorization denied");
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior == HandshakeBehavior.ProtocolMismatch)
                {
                    writer.Write((byte)IpcOpCode.Handshake);
                    writer.Write(IpcProtocol.ProtocolVersion + 1);
                    stream.Flush();
                    return;
                }

                if (_handshakeBehavior == HandshakeBehavior.NoResponse)
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
                            _commandSignal.Release();
                            _startCaptureCount++;
                            if (_handshakeBehavior == HandshakeBehavior.FailFirstStartAfterDelay &&
                                _startCaptureCount == 1)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
                                if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                                {
                                    return;
                                }
                                break;
                            }

                            if (_handshakeBehavior == HandshakeBehavior.FailSecondStartAfterDelay &&
                                _startCaptureCount == 2)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
                                if (!TryWriteCaptureStartFailed(writer, stream, requestId, "Simulated delayed start failure"))
                                {
                                    return;
                                }
                                break;
                            }

                            if (_handshakeBehavior == HandshakeBehavior.DelayAllCaptureStartAcks)
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
                            _commandSignal.Release();
                            break;
                        case IpcOpCode.SimulateEvent:
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                Type: reader.ReadUInt16(),
                                Code: reader.ReadUInt16(),
                                Value: reader.ReadInt32()));
                            _commandSignal.Release();
                            break;
                        case IpcOpCode.ConfigureResolution:
                            _commands.Enqueue(new CapturedCommand(
                                OpCode: opCode,
                                Width: reader.ReadInt32(),
                                Height: reader.ReadInt32()));
                            _commandSignal.Release();
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

            if (_serverTask != null)
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
