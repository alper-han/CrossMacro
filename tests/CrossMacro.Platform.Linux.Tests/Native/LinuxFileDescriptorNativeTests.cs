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
        Assert.Equal(payload, received);
    }

    [Fact]
    public void WriteAll_WhenReaderNeverDrainsAndTokenIsCancelled_StopsPromptly()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        LinuxFileDescriptorNative.SetNonBlocking(writeFileDescriptor);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300), TimeProvider.System);
        try
        {
            _ = Assert.ThrowsAny<OperationCanceledException>(() =>
                LinuxFileDescriptorNative.WriteAll(writeFileDescriptor, new byte[4 * 1024 * 1024], cancellation.Token));
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public async Task ReadToEnd_ReadsEverythingUntilTheWriterCloses()
    {
        var payload = new byte[(256 * 1024) + 3];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index % 241);
        }

        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        LinuxFileDescriptorNative.SetNonBlocking(writeFileDescriptor);
        var writer = Task.Run(
            () =>
            {
                try
                {
                    LinuxFileDescriptorNative.WriteAll(writeFileDescriptor, payload, CancellationToken.None);
                }
                finally
                {
                    LinuxFileDescriptorNative.Close(writeFileDescriptor);
                }
            },
            CancellationToken.None);

        try
        {
            var received = LinuxFileDescriptorNative.ReadToEnd(
                readFileDescriptor,
                maxBytes: 1024 * 1024,
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.Equal(payload, received);
            await writer;
        }
        finally
        {
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public void ReadToEnd_WhenWriterStaysSilent_TimesOut()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        try
        {
            _ = Assert.Throws<TimeoutException>(() =>
                LinuxFileDescriptorNative.ReadToEnd(
                    readFileDescriptor,
                    maxBytes: 1024,
                    TimeSpan.FromMilliseconds(200),
                    CancellationToken.None));
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public void ReadToEnd_WhenMoreThanTheLimitArrives_Throws()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        try
        {
            LinuxFileDescriptorNative.WriteAll(writeFileDescriptor, new byte[4096], CancellationToken.None);

            _ = Assert.Throws<IOException>(() =>
                LinuxFileDescriptorNative.ReadToEnd(
                    readFileDescriptor,
                    maxBytes: 1024,
                    TimeSpan.FromSeconds(5),
                    CancellationToken.None));
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
    }

    [Fact]
    public void ReadToEnd_WhenCancelled_Throws()
    {
        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200), TimeProvider.System);
        try
        {
            _ = Assert.ThrowsAny<OperationCanceledException>(() =>
                LinuxFileDescriptorNative.ReadToEnd(
                    readFileDescriptor,
                    maxBytes: 1024,
                    TimeSpan.FromMinutes(1),
                    cancellation.Token));
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
            LinuxFileDescriptorNative.Close(readFileDescriptor);
        }
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
