
namespace CrossMacro.Daemon.Tests.Services;

public sealed class CaptureForwardingCoordinatorTests
{
    [Fact]
    public async Task DrainAsync_WhenQueuedEventsAreDropped_CompletesAfterRemainingEventsForward()
    {
        using var readerStream = new MemoryStream();
        using var writerStream = new MemoryStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(reader, writer, writerStream, maxBufferedCaptureEvents: 2);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        _ = coordinator.ActivateGeneration(generation);
        var firstWriteIssued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.WriterGate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                _ = firstWriteIssued.TrySetResult();
            }
        };
        var firstWriter = await session.WriterGate.EnterAsync(CancellationToken.None);

        var forwarder = coordinator.CreateEventForwarder(generation, session);
        var firstForward = Task.Run(() => forwarder(CreateEvent(1)), CancellationToken.None);
        await firstWriteIssued.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
        forwarder(CreateEvent(2));
        forwarder(CreateEvent(3));
        forwarder(CreateEvent(4));
        var drain = coordinator.DrainAsync();

        firstWriter.Dispose();
        await firstForward.WaitAsync(TimeSpan.FromSeconds(2));
        await drain.WaitAsync(TimeSpan.FromSeconds(2));

        writer.Flush();
        writerStream.Position = 0;
        using var outputReader = new BinaryReader(writerStream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (var expectedCode in new[] { 1, 3, 4 })
        {
            Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)outputReader.ReadByte());
            Assert.Equal((byte)InputEventType.Key, outputReader.ReadByte());
            Assert.Equal(expectedCode, outputReader.ReadInt32());
            Assert.Equal(1, outputReader.ReadInt32());
            Assert.True(outputReader.ReadInt64() > 0);
        }

        Assert.Equal(writerStream.Length, outputReader.BaseStream.Position);
        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenWriterGateIsStalled_CompletesAndCancelsPendingWrite()
    {
        using var readerStream = new MemoryStream();
        using var writerStream = new MemoryStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(reader, writer, writerStream, maxBufferedCaptureEvents: 2);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        _ = coordinator.ActivateGeneration(generation);
        var firstWriter = await session.WriterGate.EnterAsync();
        var writeQueued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.WriterGate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                _ = writeQueued.TrySetResult();
            }
        };

        try
        {
            var forwarder = coordinator.CreateEventForwarder(generation, session);
            forwarder(CreateEvent(1));
            await writeQueued.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await coordinator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0, writerStream.Length);
        }
        finally
        {
            firstWriter.Dispose();
        }
    }

    [Fact]
    public async Task DrainAsync_WhenCanceledWhileWriteIsStalled_ShouldStopWaiting()
    {
        using var readerStream = new MemoryStream();
        using var writerStream = new MemoryStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(reader, writer, writerStream, maxBufferedCaptureEvents: 2);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        _ = coordinator.ActivateGeneration(generation);
        using var firstWriter = await session.WriterGate.EnterAsync();

        var writeQueued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.WriterGate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                _ = writeQueued.TrySetResult();
            }
        };

        var forwarder = coordinator.CreateEventForwarder(generation, session);
        forwarder(CreateEvent(1));
        await writeQueued.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using var cancellation = new CancellationTokenSource();
        var drain = coordinator.DrainAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => drain);

        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAfterDispose_IsIgnored()
    {
        using var readerStream = new MemoryStream();
        using var writerStream = new MemoryStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(reader, writer, writerStream, maxBufferedCaptureEvents: 2);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        var forwarder = coordinator.CreateEventForwarder(generation, session);

        await coordinator.DisposeAsync();

        forwarder(CreateEvent(1));
        var lateForwarder = coordinator.CreateEventForwarder(generation, session);
        lateForwarder(CreateEvent(2));

        Assert.Equal(0, writerStream.Length);
    }

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
            maxBufferedCaptureEvents: 16);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        _ = coordinator.ActivateGeneration(generation);

        var queuedWriteIssued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.WriterGate.TicketIssued = ticket =>
        {
            if (ticket == 1)
            {
                _ = queuedWriteIssued.TrySetResult();
            }
        };

        var firstWriter = await session.WriterGate.EnterAsync();
        var firstWriterReleased = false;

        try
        {
            var forwarder = coordinator.CreateEventForwarder(generation, session);
            var forwardTask = Task.Factory.StartNew(
                () => forwarder(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
                {
                    type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
                    code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT,
                    value = 1,
                }),
                TaskCreationOptions.LongRunning);

            await queuedWriteIssued.Task.WaitAsync(TimeSpan.FromSeconds(10));

            coordinator.Stop();
            firstWriter.Dispose();
            firstWriterReleased = true;

            await forwardTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(0, writerStream.Length);
        }
        finally
        {
            if (!firstWriterReleased)
            {
                firstWriter.Dispose();
            }
        }
    }

    [Fact]
    public async Task ForwardSynReport_HoldsWriterGateUntilFlushCompletes()
    {
        using var readerStream = new MemoryStream();
        await using var writerStream = new FlushBlockingStream();
        using var reader = new BinaryReader(readerStream);
        using var writer = new BinaryWriter(writerStream);
        var session = new DaemonProtocolSession(reader, writer, writerStream, maxBufferedCaptureEvents: 2);
        var coordinator = session.CaptureForwarding;
        var generation = coordinator.BeginPendingGeneration();
        _ = coordinator.ActivateGeneration(generation);

        try
        {
            var forwarder = coordinator.CreateEventForwarder(generation, session);
            forwarder(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
            {
                type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN,
                code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT,
            });

            await writerStream.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var controlWriter = session.WriterGate.EnterAsync().AsTask();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            Assert.False(controlWriter.IsCompleted);

            writerStream.AllowFlush();
            using (await controlWriter.WaitAsync(TimeSpan.FromSeconds(2)))
            {
                writer.Write((byte)IpcOpCode.CaptureStarted);
            }

            await coordinator.DrainAsync().WaitAsync(TimeSpan.FromSeconds(2));

            writerStream.Position = 0;
            using var outputReader = new BinaryReader(writerStream, System.Text.Encoding.UTF8, leaveOpen: true);
            Assert.Equal(IpcOpCode.InputEvent, (IpcOpCode)outputReader.ReadByte());
            _ = outputReader.ReadByte();
            _ = outputReader.ReadInt32();
            _ = outputReader.ReadInt32();
            _ = outputReader.ReadInt64();
            Assert.Equal(IpcOpCode.CaptureStarted, (IpcOpCode)outputReader.ReadByte());
        }
        finally
        {
            writerStream.AllowFlush();
            await coordinator.DisposeAsync();
        }
    }

    private static CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event CreateEvent(ushort code)
    {
        return new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = code,
            value = 1,
        };
    }

    private sealed class FlushBlockingStream : MemoryStream
    {
        private readonly TaskCompletionSource _allowFlush = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FlushStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            _ = FlushStarted.TrySetResult();
            await _allowFlush.Task.WaitAsync(cancellationToken);
        }

        public void AllowFlush() => _allowFlush.TrySetResult();
    }
}
