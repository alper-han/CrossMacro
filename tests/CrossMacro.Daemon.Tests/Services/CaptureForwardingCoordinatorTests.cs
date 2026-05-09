using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native.UInput;

namespace CrossMacro.Daemon.Tests.Services;

public sealed class CaptureForwardingCoordinatorTests
{
    [Fact]
    public async Task ForwardEvent_WhenStopRunsBeforeQueuedWrite_ShouldDropStaleEvent()
    {
        using var readerStream = new MemoryStream();
        using var writerStream = new MemoryStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(
            reader,
            writer,
            writerStream,
            maxBufferedCaptureEvents: 16,
            new DaemonInputEventEncoder());
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        coordinator.ActivateGeneration(generation);

        var queuedWriteIssued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.WriterGate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                queuedWriteIssued.TrySetResult();
            }
        };

        using var firstWriter = session.WriterGate.Enter();
        var forwarder = coordinator.CreateEventForwarder(generation, session);
        var forwardTask = Task.Run(() => forwarder(new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = UInputNative.BTN_LEFT,
            value = 1
        }));

        await queuedWriteIssued.Task.WaitAsync(TimeSpan.FromSeconds(2));

        coordinator.Stop();
        firstWriter.Dispose();
        await forwardTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, writerStream.Length);
    }
}
