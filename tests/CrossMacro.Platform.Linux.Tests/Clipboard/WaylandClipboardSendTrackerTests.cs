using CrossMacro.Platform.Linux.Clipboard;
using CrossMacro.Platform.Linux.Native;

namespace CrossMacro.Platform.Linux.Tests.Clipboard;

public sealed class WaylandClipboardSendTrackerTests
{
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(5);

    [Fact]
    public void TryStart_PayloadLargerThanPipeCapacity_TransfersEveryByteAndClosesDescriptor()
    {
        var payload = new byte[(1024 * 1024) + 17];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        using var tracker = new WaylandClipboardSendTracker(TimeSpan.FromSeconds(30));
        try
        {
            Assert.True(tracker.TryStart(writeFileDescriptor, payload));

            // The read only ends when the transfer closes its descriptor.
            var received = LinuxFileDescriptorNative.ReadToEnd(readFileDescriptor, payload.Length * 2, ReadBudget, CancellationToken.None);
            Assert.Equal(payload, received);
        }
        finally
        {
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public async Task TryStart_WhenReaderNeverConsumes_AbandonsTheTransferAtTheDeadlineAndClosesDescriptor()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        using var tracker = new WaylandClipboardSendTracker(TimeSpan.FromMilliseconds(300));
        try
        {
            Assert.True(tracker.TryStart(writeFileDescriptor, new byte[4 * 1024 * 1024]));

            // Nothing reads until the deadline has passed, so the transfer stalls on a full pipe.
            await Task.Delay(TimeSpan.FromMilliseconds(800), TimeProvider.System, CancellationToken.None);

            // End of file only arrives after the abandoned transfer closed the write end.
            var received = LinuxFileDescriptorNative.ReadToEnd(readFileDescriptor, 8 * 1024 * 1024, ReadBudget, CancellationToken.None);

            Assert.InRange(received.Length, 1, (4 * 1024 * 1024) - 1);
        }
        finally
        {
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public async Task Dispose_CancelsStalledTransfersAndWaitsForThemToClose()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        var tracker = new WaylandClipboardSendTracker(TimeSpan.FromMinutes(10));
        try
        {
            Assert.True(tracker.TryStart(writeFileDescriptor, new byte[4 * 1024 * 1024]));
            await Task.Delay(TimeSpan.FromMilliseconds(200), TimeProvider.System, CancellationToken.None);

            tracker.Dispose();

            var received = LinuxFileDescriptorNative.ReadToEnd(readFileDescriptor, 8 * 1024 * 1024, ReadBudget, CancellationToken.None);
            Assert.True(received.Length < 4 * 1024 * 1024);
        }
        finally
        {
            tracker.Dispose();
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public void TryStart_AfterDispose_ClosesTheDescriptorAndSendsNothing()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        var tracker = new WaylandClipboardSendTracker(TimeSpan.FromSeconds(30));
        tracker.Dispose();
        try
        {
            Assert.False(tracker.TryStart(writeFileDescriptor, [1, 2, 3]));

            var received = LinuxFileDescriptorNative.ReadToEnd(readFileDescriptor, 1024, ReadBudget, CancellationToken.None);
            Assert.Empty(received);
        }
        finally
        {
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }
}
