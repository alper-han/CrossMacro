namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsClipboardReaderTests
{
    private const uint PngFormat = 1001;
    private const uint ImagePngFormat = 1002;

    [Fact]
    public void ReadPng_UsesImagePngFallbackAndAlwaysUnlocksAndCloses()
    {
        var pngBytes = new byte[] { 137, 80, 78, 71 };
        using var native = new FakeClipboardNative();
        native.SetReadable(ImagePngFormat, pngBytes);

        var result = new WindowsClipboardReader(native).ReadPng(1024, PngFormat, ImagePngFormat, IntPtr.Zero);

        Assert.Equal(pngBytes, result);
        Assert.Equal(1, native.UnlockCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void ReadPng_WhenNoPngFormatIsAvailable_ReturnsNullWithoutOpeningTheClipboard()
    {
        using var native = new FakeClipboardNative { CanOpen = false };

        var result = new WindowsClipboardReader(native).ReadPng(1024, PngFormat, ImagePngFormat, IntPtr.Zero);

        Assert.Null(result);
        Assert.Equal(0, native.CloseCalls);
    }

    [Fact]
    public void ReadPng_WhenDataExceedsLimit_ClosesClipboardBeforeThrowing()
    {
        using var native = new FakeClipboardNative();
        native.SetReadable(PngFormat, [1, 2, 3, 4]);

        _ = Assert.Throws<InvalidDataException>(
            () => new WindowsClipboardReader(native).ReadPng(3, PngFormat, ImagePngFormat, IntPtr.Zero));

        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void ReadPng_WhenClipboardCannotOpen_ThrowsWithoutClosing()
    {
        using var native = new FakeClipboardNative { CanOpen = false };
        native.SetReadable(PngFormat, [1, 2, 3, 4]);

        _ = Assert.Throws<InvalidOperationException>(
            () => new WindowsClipboardReader(native).ReadPng(1024, PngFormat, ImagePngFormat, IntPtr.Zero));

        Assert.Equal(0, native.CloseCalls);
    }
}
