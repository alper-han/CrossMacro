using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Native.X11;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class X11ScreenCaptureTests
{
    [Fact]
    public async Task CaptureSupportedAsync_WhenNativeImageReturned_CopiesRgbPixelsAndCleansUpNativeHandles()
    {
        var native = new FakeX11NativeApi
        {
            ImageData = new XImage
            {
                Width = 2,
                Height = 1,
                RedMask = new UIntPtr(0x00FF0000UL),
                GreenMask = new UIntPtr(0x0000FF00UL),
                BlueMask = new UIntPtr(0x000000FFUL)
            }
        };
        native.Pixels[(0, 0)] = new UIntPtr(0x00112233UL);
        native.Pixels[(1, 0)] = new UIntPtr(0x00445566UL);
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
        var native = new FakeX11NativeApi
        {
            RootWidth = 20,
            RootHeight = 10
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
        var native = new FakeX11NativeApi
        {
            ImageData = new XImage
            {
                Width = 1,
                Height = 1,
                RedMask = UIntPtr.Zero,
                GreenMask = UIntPtr.Zero,
                BlueMask = UIntPtr.Zero
            }
        };
        using var capture = CreateCapture(native);

        var result = await capture.CaptureSupportedAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Contains("RGB channel masks", result.ErrorMessage);
        Assert.Equal(1, native.DestroyImageCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public async Task CaptureAsync_WhenProbeUnsupported_DoesNotOpenDisplay()
    {
        var native = new FakeX11NativeApi();
        using var capture = new X11ScreenCapture(
            new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Unsupported("DISPLAY missing")),
            native);

        var result = await capture.CaptureAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY missing", result.ErrorMessage);
        Assert.Equal(0, native.OpenDisplayCalls);
    }

    [Fact]
    public void ProbeSupport_WhenDisplayIsMissing_DoesNotOpenDisplay()
    {
        var native = new FakeX11NativeApi();
        var probe = new X11ScreenCaptureSupportProbe(native, _ => null);

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY", result.ErrorMessage);
        Assert.Equal(0, native.OpenDisplayCalls);
    }

    private static X11ScreenCapture CreateCapture(FakeX11NativeApi native) =>
        new(new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Supported()), native);

    private sealed class FakeX11ScreenCaptureSupportProbe : IX11ScreenCaptureSupportProbe
    {
        private readonly X11ScreenCaptureSupportResult _support;

        public FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult support)
        {
            _support = support;
        }

        public X11ScreenCaptureSupportResult ProbeSupport() => _support;
    }

    private sealed class FakeX11NativeApi : IX11NativeApi
    {
        private readonly IntPtr _display = new(1);
        private readonly IntPtr _root = new(2);
        private readonly IntPtr _image = new(3);

        public uint RootWidth { get; init; } = 100;

        public uint RootHeight { get; init; } = 80;

        public XImage ImageData { get; init; } = new()
        {
            Width = 1,
            Height = 1,
            RedMask = new UIntPtr(0x00FF0000UL),
            GreenMask = new UIntPtr(0x0000FF00UL),
            BlueMask = new UIntPtr(0x000000FFUL)
        };

        public Dictionary<(int X, int Y), UIntPtr> Pixels { get; } = [];

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

        public UIntPtr GetPixel(IntPtr ximage, int x, int y)
        {
            Assert.Equal(_image, ximage);
            return Pixels.TryGetValue((x, y), out var pixel) ? pixel : UIntPtr.Zero;
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
            return ImageData;
        }
    }
}
