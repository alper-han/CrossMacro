using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using CrossMacro.Core.Ipc;
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

        var callbacksObserved = 0;
        client.ErrorOccurred += (_, _) =>
        {
            Interlocked.Increment(ref callbacksObserved);
            client.StartCapture("stress-consumer", mouse: true, keyboard: true);
            client.StopCapture("stress-consumer");
        };

        const int iterations = 50;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            InvokeHandleSendFailureWhileHoldingGate(
                client,
                captureGate,
                handleSendFailureMethod!,
                new IOException($"Simulated send failure {iteration}"),
                pendingCallback: null);
        }

        var waitDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (Volatile.Read(ref callbacksObserved) < iterations && DateTime.UtcNow < waitDeadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.True(Volatile.Read(ref callbacksObserved) >= iterations);
    }

    private static void InvokeHandleSendFailureWhileHoldingGate(
        IpcClient client,
        SemaphoreSlim captureGate,
        MethodInfo handleSendFailureMethod,
        IOException sendFailure,
        Task? pendingCallback)
    {
        captureGate.Wait();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var invocationException = Record.Exception(() =>
            {
                handleSendFailureMethod.Invoke(
                    client,
                    [sendFailure, IpcOpCode.StartCapture, false]);
            });
            stopwatch.Stop();

            Assert.Null(invocationException);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"HandleSendFailure should return promptly while the capture gate is held. Elapsed: {stopwatch.Elapsed}.");

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
