using System.Text;

namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsClipboardWriterTests
{
    private const uint PngFormat = 1001;
    private const uint ImagePngFormat = 1002;
    private static readonly byte[] PngBytes = [137, 80, 78, 71];

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    public void WritePng_WhenOpeningOrEmptyingFails_FreesEveryPreparedFormat(bool canOpen, bool canEmpty, int closeCalls)
    {
        using var native = new FakeClipboardNative { CanOpen = canOpen, CanEmpty = canEmpty };
        var writer = new WindowsClipboardWriter(native);

        _ = Assert.Throws<InvalidOperationException>(() => WritePng(writer, native));

        Assert.Equal(3, native.Allocated.Count);
        Assert.Equal(3, native.Freed.Count);
        Assert.Empty(native.Published);
        Assert.Equal(closeCalls, native.CloseCalls);
    }

    [Theory]
    [InlineData(PngFormat)]
    [InlineData(ImagePngFormat)]
    [InlineData(User32.CF_DIB)]
    public void WritePng_WhenOneFormatIsRejected_OnlyReclaimsThatFormat(uint rejectedFormat)
    {
        using var native = new FakeClipboardNative { RejectedFormat = rejectedFormat };

        WritePng(new WindowsClipboardWriter(native), native);

        Assert.Equal(2, native.Published.Count);
        _ = Assert.Single(native.Freed);
        Assert.DoesNotContain(rejectedFormat, native.Published.Keys);
        Assert.All(native.Transferred, handle => Assert.DoesNotContain(handle, native.Freed));
        Assert.Equal(1, native.CloseCalls);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void WritePng_WhenAllocationOrLockFails_SkipsOnlyTheUnavailableFormat(int failedAllocation, bool failLock)
    {
        using var native = new FakeClipboardNative
        {
            FailedAllocation = failLock ? 0 : failedAllocation,
            FailedLock = failLock ? failedAllocation : 0,
        };

        WritePng(new WindowsClipboardWriter(native), native);

        Assert.Equal(2, native.Published.Count);
        Assert.Equal(failLock ? 1 : 0, native.Freed.Count);
        var skippedFormat = failedAllocation is 2 ? PngFormat : ImagePngFormat;
        Assert.DoesNotContain(skippedFormat, native.Published.Keys);
        var publishedFormat = failedAllocation is 2 ? ImagePngFormat : PngFormat;
        Assert.Equal(PngBytes, native.Published[publishedFormat]);
        Assert.Equal(1, native.UnlockCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void WritePng_WhenPublishingThrows_KeepsEarlierTransferAndFreesTheRest()
    {
        using var native = new FakeClipboardNative { ThrowOnFormat = ImagePngFormat };

        _ = Assert.Throws<InvalidOperationException>(() => WritePng(new WindowsClipboardWriter(native), native));

        Assert.Equal(PngBytes, native.Published[PngFormat]);
        _ = Assert.Single(native.Transferred);
        Assert.Equal(2, native.Freed.Count);
        Assert.DoesNotContain(native.Transferred[0], native.Freed);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void Allocate_WhenCopyThrows_UnlocksAndFreesBeforePropagating()
    {
        using var native = new FakeClipboardNative();

        _ = Assert.Throws<InvalidOperationException>(() => WindowsClipboardMemory.TryAllocate(
            native, byteCount: 4, _ => throw new InvalidOperationException("copy failed")));

        _ = Assert.Single(native.Freed);
        Assert.Equal(1, native.UnlockCalls);
        Assert.Empty(native.Transferred);
    }

    [Fact]
    public void OwnedMemory_WhenDisposedTwice_FreesOnceAndCannotPublish()
    {
        using var native = new FakeClipboardNative();
        var memory = Assert.IsType<WindowsClipboardMemory>(WindowsClipboardMemory.TryAllocate(native, byteCount: 4, _ => { }));

        memory.Dispose();
        memory.Dispose();

        _ = Assert.Single(native.Freed);
        _ = Assert.Throws<ObjectDisposedException>(() => memory.TryPublish(PngFormat));
        Assert.Empty(native.Transferred);
    }

    [Fact]
    public void WriteText_WritesTerminatedUtf16AndTransfersOwnership()
    {
        using var native = new FakeClipboardNative();

        new WindowsClipboardWriter(native).WriteText("İstanbul 👋\r\n", IntPtr.Zero);

        Assert.Equal(Encoding.Unicode.GetBytes("İstanbul 👋\r\n\0"), native.Published[User32.CF_UNICODETEXT]);
        Assert.Empty(native.Freed);
        Assert.Equal(1, native.UnlockCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteText_WhenAllocationOrLockFails_ClosesAndDoesNotPublish(bool failLock)
    {
        using var native = new FakeClipboardNative
        {
            FailedAllocation = failLock ? 0 : 1,
            FailedLock = failLock ? 1 : 0,
        };

        _ = Assert.Throws<InvalidOperationException>(() => new WindowsClipboardWriter(native).WriteText("text", IntPtr.Zero));

        Assert.Empty(native.Published);
        Assert.Equal(failLock ? 1 : 0, native.Freed.Count);
        Assert.Equal(0, native.UnlockCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void Clear_OpensEmptiesAndClosesWithoutPublishing()
    {
        using var native = new FakeClipboardNative();

        new WindowsClipboardWriter(native).Clear(IntPtr.Zero);

        Assert.Empty(native.Published);
        Assert.Equal(1, native.CloseCalls);
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    public void Clear_WhenOpeningOrEmptyingFails_Throws(bool canOpen, bool canEmpty, int closeCalls)
    {
        using var native = new FakeClipboardNative { CanOpen = canOpen, CanEmpty = canEmpty };

        _ = Assert.Throws<InvalidOperationException>(() => new WindowsClipboardWriter(native).Clear(IntPtr.Zero));

        Assert.Equal(closeCalls, native.CloseCalls);
    }

    private static void WritePng(WindowsClipboardWriter writer, FakeClipboardNative native)
        => writer.WritePng(PngBytes, PngFormat, ImagePngFormat, IntPtr.Zero, _ => native.Allocate(4));
}
