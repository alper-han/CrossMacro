using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientSendFailureTests
{
    [LinuxFact]
    public async Task ConnectAsync_WhenDisposedWhileWaitingForConnectGate_CannotInstallTransport()
    {
        using var client = new IpcClient(() => throw new InvalidOperationException("Socket resolver should not run."), autoReconnect: false);
        var gate = client.ConnectGate;
        Assert.True(await gate.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None));

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectTask = Task.Run(
            async () =>
            {
                started.SetResult();
                await client.ConnectAsync(CancellationToken.None);
            },
            CancellationToken.None);

        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
        client.Dispose();

        await TestAssertions.ThrowsAnyAsync<Exception>(() =>
            connectTask.WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None));
    }

    [LinuxFact]
    public async Task DeferredErrorNotification_WhenAlreadyDisposed_DoesNotInvokeHandler()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) => callbackObserved.TrySetResult();
        await client.DisposeAsync();

        client.RaiseErrorOccurredDeferred("late error");

        Assert.False(callbackObserved.Task.IsCompleted);
    }

    [LinuxFact]
    public async Task HandleSendFailure_WhenErrorHandlerReentersCaptureControl_ShouldNotBlockCaller()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGate = client.CaptureCommandGate;

        var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) =>
        {
            _ = callbackObserved.TrySetResult();
            client.StopCapture("reentrant-consumer");
        };

        await InvokeHandleSendFailureWhileHoldingGateAsync(
            client,
            captureGate,
            new IOException("Simulated send failure"),
            callbackObserved.Task);

        await callbackObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
    }

    [LinuxFact]
    public async Task HandleSendFailure_WhenOneErrorHandlerThrows_OtherHandlersStillRun()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGate = client.CaptureCommandGate;

        var healthySubscriberObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) => throw new InvalidOperationException("Simulated error callback failure");
        client.ErrorOccurred += (_, _) =>
        {
            _ = healthySubscriberObserved.TrySetResult();
            client.StopCapture("healthy-consumer");
        };

        await InvokeHandleSendFailureWhileHoldingGateAsync(
            client,
            captureGate,
            new IOException("Simulated send failure"),
            healthySubscriberObserved.Task);

        await healthySubscriberObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
    }

    [LinuxFact]
    public async Task HandleSendFailure_WhenReenteredRepeatedly_ShouldNotDeadlock()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGate = client.CaptureCommandGate;

        const int iterations = 50;
        var callbacksObserved = 0;
        TaskCompletionSource? nextCallbackObserved = null;
        client.ErrorOccurred += (_, _) =>
        {
            _ = Interlocked.Increment(ref callbacksObserved);
            _ = (Volatile.Read(ref nextCallbackObserved)?.TrySetResult());

            client.StartCapture("stress-consumer", mouse: true, keyboard: true);
            client.StopCapture("stress-consumer");
        };
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref nextCallbackObserved, callbackObserved);

            await InvokeHandleSendFailureWhileHoldingGateAsync(
                client,
                captureGate,
                new IOException(string.Create(CultureInfo.InvariantCulture, $"Simulated send failure {iteration}")),
                pendingCallback: null);

            await callbackObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None);
            Assert.Equal(iteration + 1, Volatile.Read(ref callbacksObserved));
        }

        Assert.Equal(iterations, Volatile.Read(ref callbacksObserved));
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenDeferredReconcileIsWaitingForGate_ShouldCancelWithoutLeakingCancellation()
    {
        await using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var captureGate = client.CaptureCommandGate;
        Assert.True(await captureGate.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None));

        var reconcileTask = client.StartDeferredCaptureReconcileAsync();

        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeTask = Task.Run(
            async () =>
            {
                disposeStarted.SetResult();
                await client.DisposeAsync();
            },
            CancellationToken.None);
        await disposeStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
        await disposeTask.WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);

        var exception = await Record.ExceptionAsync(() =>
            reconcileTask.WaitAsync(
                TimeSpan.FromSeconds(2),
                TimeProvider.System,
                CancellationToken.None));
        Assert.Null(exception);
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCalledConcurrently_ShouldShareCleanupTask()
    {
        await using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var firstDispose = DisposeClientAsync(client);
        var secondDispose = DisposeClientAsync(client);

        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);

        var sharedDisposeTask = client.DisposeTask;
        Assert.NotNull(sharedDisposeTask);
        Assert.True(sharedDisposeTask.IsCompleted);
        Assert.True(firstDispose.IsCompletedSuccessfully);
        Assert.True(secondDispose.IsCompletedSuccessfully);
    }

    private static async Task DisposeClientAsync(IpcClient client)
    {
        await client.DisposeAsync();
    }

    private static async Task InvokeHandleSendFailureWhileHoldingGateAsync(
        IpcClient client,
        SemaphoreSlim captureGate,
        IOException sendFailure,
        Task? pendingCallback)
    {
        Assert.True(
            await captureGate.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None),
            "Timed out waiting to acquire the capture command gate.");
        try
        {
            var invocationException = Record.Exception(() =>
                client.HandleSendFailureForSession(sendFailure, IpcOpCode.StartCapture, throwOnFailure: false, sessionGeneration: null));

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
