using System.IO;
using System.Reflection;
using System.Threading;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class IpcClientSendFailureTests
{
    [LinuxFact]
    public async Task HandleSendFailure_WhenErrorHandlerReentersCaptureControl_ShouldNotBlockCaller()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGateField = typeof(IpcClient).GetField(
            "_captureCommandGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(captureGateField);
        var captureGate = Assert.IsType<SemaphoreSlim>(captureGateField!.GetValue(client));

        var handleSendFailureMethod = typeof(IpcClient).GetMethod(
            "HandleSendFailure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleSendFailureMethod);

        var callbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) =>
        {
            callbackObserved.TrySetResult();
            client.StopCapture("reentrant-consumer");
        };

        InvokeHandleSendFailureWhileHoldingGate(
            client,
            captureGate,
            handleSendFailureMethod!,
            new IOException("Simulated send failure"),
            callbackObserved.Task);

        await callbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task HandleSendFailure_WhenOneErrorHandlerThrows_OtherHandlersStillRun()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGateField = typeof(IpcClient).GetField(
            "_captureCommandGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(captureGateField);
        var captureGate = Assert.IsType<SemaphoreSlim>(captureGateField!.GetValue(client));

        var handleSendFailureMethod = typeof(IpcClient).GetMethod(
            "HandleSendFailure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleSendFailureMethod);

        var healthySubscriberObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += (_, _) => throw new InvalidOperationException("Simulated error callback failure");
        client.ErrorOccurred += (_, _) =>
        {
            healthySubscriberObserved.TrySetResult();
            client.StopCapture("healthy-consumer");
        };

        InvokeHandleSendFailureWhileHoldingGate(
            client,
            captureGate,
            handleSendFailureMethod!,
            new IOException("Simulated send failure"),
            healthySubscriberObserved.Task);

        await healthySubscriberObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [LinuxFact]
    public async Task HandleSendFailure_WhenReenteredRepeatedly_ShouldNotDeadlock()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var captureGateField = typeof(IpcClient).GetField(
            "_captureCommandGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(captureGateField);
        var captureGate = Assert.IsType<SemaphoreSlim>(captureGateField!.GetValue(client));

        var handleSendFailureMethod = typeof(IpcClient).GetMethod(
            "HandleSendFailure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(handleSendFailureMethod);

        const int iterations = 50;
        var callbacksObserved = 0;
        TaskCompletionSource? nextCallbackObserved = null;
        client.ErrorOccurred += (_, _) =>
        {
            Interlocked.Increment(ref callbacksObserved);
            Volatile.Read(ref nextCallbackObserved)?.TrySetResult();

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
                handleSendFailureMethod!,
                new IOException($"Simulated send failure {iteration}"),
                pendingCallback: null);

            await callbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(iteration + 1, Volatile.Read(ref callbacksObserved));
        }

        Assert.Equal(iterations, Volatile.Read(ref callbacksObserved));
    }

    private static void InvokeHandleSendFailureWhileHoldingGate(
        IpcClient client,
        SemaphoreSlim captureGate,
        MethodInfo handleSendFailureMethod,
        IOException sendFailure,
        Task? pendingCallback)
    {
        Assert.True(captureGate.Wait(TimeSpan.FromSeconds(2)), "Timed out waiting to acquire the capture command gate.");
        try
        {
            var invocationException = Record.Exception(() =>
            {
                handleSendFailureMethod.Invoke(
                    client,
                    [sendFailure, IpcOpCode.StartCapture, false]);
            });

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
            captureGate.Release();
        }
    }
}
