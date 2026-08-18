using CrossMacro.Platform.Linux.Clipboard;
using CrossMacro.Platform.Linux.Native;

namespace CrossMacro.Platform.Linux.Tests.Native;

public sealed class LinuxFileDescriptorNativeTests
{
    [Fact]
    public async Task WriteAll_PayloadLargerThanPipeCapacity_TransfersEveryByte()
    {
        var payload = new byte[(1024 * 1024) + 17];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        LinuxFileDescriptorNative.SetNonBlocking(writeFileDescriptor);
        var readTask = Task.Run(
            () => ReadToEnd(readFileDescriptor),
            CancellationToken.None);

        try
        {
            LinuxFileDescriptorNative.WriteAll(writeFileDescriptor, payload, CancellationToken.None);
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), TimeProvider.System);
        var received = await readTask.WaitAsync(timeout.Token);
        received.Should().Equal(payload);
    }

    [Fact]
    public async Task ClipboardSend_PayloadLargerThanPipeCapacity_TransfersEveryByteAndClosesDescriptor()
    {
        var payload = new byte[(1024 * 1024) + 17];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        LinuxFileDescriptorNative.SetNonBlocking(writeFileDescriptor);
        var readTask = Task.Run(
            () => ReadToEnd(readFileDescriptor),
            CancellationToken.None);

        WaylandClipboardConnection.HandleSourceSend(writeFileDescriptor, payload);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), TimeProvider.System);
        var received = await readTask.WaitAsync(timeout.Token);
        received.Should().Equal(payload);
    }

    private static byte[] ReadToEnd(int fileDescriptor)
    {
        using var stream = new FileStream(
            new SafeFileHandle((IntPtr)fileDescriptor, ownsHandle: true),
            FileAccess.Read,
            bufferSize: 16 * 1024,
            isAsync: false);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
