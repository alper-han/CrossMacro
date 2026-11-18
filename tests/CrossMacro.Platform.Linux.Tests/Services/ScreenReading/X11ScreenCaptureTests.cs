
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class X11ScreenCaptureTests
{
    [Fact]
    public async Task CaptureSupportedAsync_WhenNativeImageReturned_CopiesRgbPixelsAndCleansUpNativeHandles()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(2, 1, bitsPerPixel: 32, bytesPerLine: 8),
        };
        native.SetImageBytes([0x33, 0x22, 0x11, 0x00, 0x66, 0x55, 0x44, 0x00]);
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(10, 20, 2, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        var frame = Assert.IsType<X11ScreenCaptureFrame>(result.Frame);
        Assert.Equal(new ScreenRect(10, 20, 2, 1), frame.LogicalBounds);
        Assert.Equal(8, frame.Stride);
        Assert.Equal(ScreenPixelFormat.Xrgb8888, frame.PixelFormat);
        Assert.Equal([0x33, 0x22, 0x11, 0x00, 0x66, 0x55, 0x44, 0x00], frame.Pixels.ToArray());
        Assert.Equal(10, native.LastGetImageX);
        Assert.Equal(20, native.LastGetImageY);
        Assert.Equal(2U, native.LastGetImageWidth);
        Assert.Equal(1U, native.LastGetImageHeight);
        Assert.Equal(1, native.DestroyImageCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenRegionOutsideRoot_ReturnsOutOfBoundsWithoutCapturingImage()
    {
        using var native = new FakeX11NativeApi
        {
            RootWidth = 20,
            RootHeight = 10,
        };
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(19, 0, 2, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.OutOfBounds, result.ErrorKind);
        Assert.Equal(0, native.GetImageCalls);
        Assert.Equal(0, native.DestroyImageCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenImageCopyFails_StillDestroysImageAndClosesDisplay()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(1, 1, bitsPerPixel: 32, bytesPerLine: 4) with
            {
                RedMask = UIntPtr.Zero,
                GreenMask = UIntPtr.Zero,
                BlueMask = UIntPtr.Zero,
            },
        };
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("RGB channel masks", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, native.DestroyImageCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public async Task CaptureAsync_WhenProbeUnsupported_DoesNotOpenDisplay()
    {
        using var native = new FakeX11NativeApi();
        using var capture = new X11ScreenCapture(
            new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Unsupported("DISPLAY missing")),
            native);

        var result = await capture.CaptureAsync(region: null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY missing", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, native.OpenDisplayCalls);
    }

    [Fact]
    public void ProbeSupport_WhenDisplayIsMissing_DoesNotOpenDisplay()
    {
        using var native = new FakeX11NativeApi();
        var probe = new X11ScreenCaptureSupportProbe(native, _ => null);

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, native.OpenDisplayCalls);
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenRowsArePadded_ReadsEachRowAtItsOwnStride()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(1, 2, bitsPerPixel: 32, bytesPerLine: 8),
        };
        native.SetImageBytes([0x03, 0x02, 0x01, 0x00, 0xEE, 0xEE, 0xEE, 0xEE, 0x13, 0x12, 0x11, 0x00, 0xEE, 0xEE, 0xEE, 0xEE]);
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 2), ScreenReadOptions.Default);

        var frame = Assert.IsType<X11ScreenCaptureFrame>(result.Frame);
        Assert.Equal(4, frame.Stride);
        Assert.Equal([0x03, 0x02, 0x01, 0x00, 0x13, 0x12, 0x11, 0x00], frame.Pixels.ToArray());
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenServerIsMostSignificantByteFirst_DecodesPixelsBigEndian()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(1, 1, bitsPerPixel: 32, bytesPerLine: 4) with { ByteOrder = 1 },
        };
        native.SetImageBytes([0x00, 0x11, 0x22, 0x33]);
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        var frame = Assert.IsType<X11ScreenCaptureFrame>(result.Frame);
        Assert.Equal([0x33, 0x22, 0x11, 0x00], frame.Pixels.ToArray());
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenPixelsAre16BitRgb565_ScalesChannelsToEightBits()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(1, 1, bitsPerPixel: 16, bytesPerLine: 2) with
            {
                RedMask = new UIntPtr(0xF800UL),
                GreenMask = new UIntPtr(0x07E0UL),
                BlueMask = new UIntPtr(0x001FUL),
            },
        };
        native.SetImageBytes([0xFF, 0xFF]);
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        var frame = Assert.IsType<X11ScreenCaptureFrame>(result.Frame);
        Assert.Equal([0xFF, 0xFF, 0xFF, 0x00], frame.Pixels.ToArray());
    }

    [Fact]
    public async Task CaptureSupportedAsync_WhenBitsPerPixelIsUnsupported_FailsAndStillCleansUp()
    {
        using var native = new FakeX11NativeApi
        {
            ImageData = Image(1, 1, bitsPerPixel: 1, bytesPerLine: 4),
        };
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Contains("bits per pixel", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, native.DestroyImageCalls);
    }

    private static XImage Image(int width, int height, int bitsPerPixel, int bytesPerLine) => new()
    {
        Width = width,
        Height = height,
        BitsPerPixel = bitsPerPixel,
        BytesPerLine = bytesPerLine,
        RedMask = new UIntPtr(0x00FF0000UL),
        GreenMask = new UIntPtr(0x0000FF00UL),
        BlueMask = new UIntPtr(0x000000FFUL),
    };

    private static X11ScreenCapture CreateCapture(FakeX11NativeApi native) =>
        new(new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Supported()), native);

    private sealed class FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult support) : IX11ScreenCaptureSupportProbe
    {
        private readonly X11ScreenCaptureSupportResult _support = support;

        public X11ScreenCaptureSupportResult ProbeSupport() => _support;
    }

    private sealed class FakeX11NativeApi : IX11NativeApi, IDisposable
    {
        private IntPtr _imageBytes;

        private readonly IntPtr _display = new(1);
        private readonly IntPtr _root = new(2);
        private readonly IntPtr _image = new(3);

        public uint RootWidth { get; init; } = 100;

        public uint RootHeight { get; init; } = 80;

        public XImage ImageData { get; init; } = Image(1, 1, bitsPerPixel: 32, bytesPerLine: 4);

        /// <summary>Places the server-side pixel bytes in unmanaged memory like a real XImage does.</summary>
        public void SetImageBytes(byte[] bytes)
        {
            _imageBytes = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, _imageBytes, bytes.Length);
        }

        public void Dispose()
        {
            if (_imageBytes != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_imageBytes);
            }
        }

        public int OpenDisplayCalls { get; private set; }

        public int CloseDisplayCalls { get; private set; }

        public int GetImageCalls { get; private set; }

        public int DestroyImageCalls { get; private set; }

        public int LastGetImageX { get; private set; }

        public int LastGetImageY { get; private set; }

        public uint LastGetImageWidth { get; private set; }

        public uint LastGetImageHeight { get; private set; }

        public IntPtr OpenDisplay(string? display)
        {
            OpenDisplayCalls++;
            return _display;
        }

        public int CloseDisplay(IntPtr display)
        {
            Assert.Equal(_display, display);
            CloseDisplayCalls++;
            return 0;
        }

        public IntPtr DefaultRootWindow(IntPtr display)
        {
            Assert.Equal(_display, display);
            return _root;
        }

        public int GetGeometry(
            IntPtr display,
            IntPtr drawable,
            out IntPtr root,
            out int x,
            out int y,
            out uint width,
            out uint height,
            out uint borderWidth,
            out uint depth)
        {
            Assert.Equal(_display, display);
            Assert.Equal(_root, drawable);
            root = _root;
            x = 0;
            y = 0;
            width = RootWidth;
            height = RootHeight;
            borderWidth = 0;
            depth = 24;
            return 1;
        }

        public IntPtr GetImage(
            IntPtr display,
            IntPtr drawable,
            int x,
            int y,
            uint width,
            uint height,
            UIntPtr planeMask,
            int format)
        {
            Assert.Equal(_display, display);
            Assert.Equal(_root, drawable);
            Assert.Equal(new UIntPtr(ulong.MaxValue), planeMask);
            Assert.Equal(2, format);
            GetImageCalls++;
            LastGetImageX = x;
            LastGetImageY = y;
            LastGetImageWidth = width;
            LastGetImageHeight = height;
            return _image;
        }

        public int DestroyImage(IntPtr ximage)
        {
            Assert.Equal(_image, ximage);
            DestroyImageCalls++;
            return 0;
        }

        public XImage ReadImage(IntPtr ximage)
        {
            Assert.Equal(_image, ximage);
            return ImageData with { Data = _imageBytes };
        }
    }
}
