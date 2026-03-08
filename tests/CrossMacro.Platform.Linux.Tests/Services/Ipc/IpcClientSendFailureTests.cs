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

        captureGate.Wait();
        Task invokeTask;
        try
        {
            invokeTask = Task.Run(() =>
            {
                handleSendFailureMethod!.Invoke(
                    client,
                    [new IOException("Simulated send failure"), IpcOpCode.StartCapture, false]);
            });

            var completed = await Task.WhenAny(
                invokeTask,
                Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.Same(invokeTask, completed);
        }
        finally
        {
            captureGate.Release();
        }

        await invokeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        captureGate.Wait();
        Task invokeTask;
        try
        {
            invokeTask = Task.Run(() =>
            {
                handleSendFailureMethod!.Invoke(
                    client,
                    [new IOException("Simulated send failure"), IpcOpCode.StartCapture, false]);
            });

            var completed = await Task.WhenAny(
                invokeTask,
                Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.Same(invokeTask, completed);
        }
        finally
        {
            captureGate.Release();
        }

        await invokeTask.WaitAsync(TimeSpan.FromSeconds(2));
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
            captureGate.Wait();
            Task invokeTask;
            try
            {
                invokeTask = Task.Run(() =>
                {
                    handleSendFailureMethod!.Invoke(
                        client,
                        [new IOException($"Simulated send failure {iteration}"), IpcOpCode.StartCapture, false]);
                });

                var completed = await Task.WhenAny(
                    invokeTask,
                    Task.Delay(TimeSpan.FromMilliseconds(300)));
                Assert.Same(invokeTask, completed);
            }
            finally
            {
                captureGate.Release();
            }

            await invokeTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        var waitDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (Volatile.Read(ref callbacksObserved) < iterations && DateTime.UtcNow < waitDeadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.True(Volatile.Read(ref callbacksObserved) >= iterations);
    }
}
