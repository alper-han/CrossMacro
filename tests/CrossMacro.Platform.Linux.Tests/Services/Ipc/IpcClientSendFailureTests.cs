using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientSendFailureTests
{
    [LinuxFact]
    public async Task ConnectAsync_WhenDisposedWhileWaitingForConnectGate_CannotInstallTransport()
    {
        using var client = new IpcClient(() => throw new InvalidOperationException("Socket resolver should not run."), autoReconnect: false);
        var gate = client.ConnectGate;
        Assert.True(gate.Wait(TimeSpan.FromSeconds(2)));

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectTask = Task.Run(async () =>
        {
            started.SetResult();
            await client.ConnectAsync(CancellationToken.None);
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.Dispose();

        await TestAssertions.ThrowsAnyAsync<Exception>(() => connectTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [LinuxFact]
    public async Task DeferredErrorNotification_WhenAlreadyDisposed_DoesNotInvokeHandler()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) => callbackObserved.TrySetResult();
        client.Dispose();

        client.RaiseErrorOccurredDeferred("late error");

        await Task.Delay(TimeSpan.FromMilliseconds(100));
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

        InvokeHandleSendFailureWhileHoldingGate(
            client,
            captureGate,
            new IOException("Simulated send failure"),
            callbackObserved.Task);

        await callbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

        InvokeHandleSendFailureWhileHoldingGate(
            client,
            captureGate,
            new IOException("Simulated send failure"),
            healthySubscriberObserved.Task);

        await healthySubscriberObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

            InvokeHandleSendFailureWhileHoldingGate(
                client,
                captureGate,
                new IOException(string.Create(CultureInfo.InvariantCulture, $"Simulated send failure {iteration}")),
                pendingCallback: null);

            await callbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(iteration + 1, Volatile.Read(ref callbacksObserved));
        }

        Assert.Equal(iterations, Volatile.Read(ref callbacksObserved));
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenDeferredReconcileIsWaitingForGate_ShouldCancelWithoutLeakingCancellation()
    {
        await using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var captureGate = client.CaptureCommandGate;
        Assert.True(captureGate.Wait(TimeSpan.FromSeconds(2)));

        var reconcileTask = client.StartDeferredCaptureReconcileAsync();

        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeTask = Task.Run(async () =>
        {
            disposeStarted.SetResult();
            await client.DisposeAsync();
        });
        await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));

        var exception = await Record.ExceptionAsync(() => reconcileTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(exception);
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCalledConcurrently_ShouldShareCleanupTask()
    {
        await using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        var firstDispose = client.DisposeAsync().AsTask();
        var secondDispose = client.DisposeAsync().AsTask();

        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(2));

        var sharedDisposeTask = client.DisposeTask;
        Assert.NotNull(sharedDisposeTask);
        Assert.True(sharedDisposeTask!.IsCompleted);
        Assert.True(firstDispose.IsCompletedSuccessfully);
        Assert.True(secondDispose.IsCompletedSuccessfully);
    }

    private static void InvokeHandleSendFailureWhileHoldingGate(
        IpcClient client,
        SemaphoreSlim captureGate,
        IOException sendFailure,
        Task? pendingCallback)
    {
        Assert.True(captureGate.Wait(TimeSpan.FromSeconds(2)), "Timed out waiting to acquire the capture command gate.");
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
