namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class X11ScreenCaptureSupportProbeTests
{
    [Fact]
    public void ProbeSupport_WhenRootWindowIsNull_DoesNotQueryGeometryAndClosesDisplay()
    {
        var native = new FakeX11NativeApi { Root = IntPtr.Zero };
        var probe = new X11ScreenCaptureSupportProbe(native, _ => ":0");

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("root window", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, native.GetGeometryCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public void ProbeSupport_WhenGeometryIsValid_ReturnsSupportedAndClosesDisplay()
    {
        var native = new FakeX11NativeApi { Width = 1920, Height = 1080 };
        var probe = new X11ScreenCaptureSupportProbe(native, _ => ":0");

        var result = probe.ProbeSupport();

        Assert.True(result.IsSupported);
        Assert.Equal(1, native.GetGeometryCalls);
        Assert.Equal(1, native.CloseDisplayCalls);
    }

    [Fact]
    public void ProbeSupport_WhenNativeLibraryIsUnavailable_ReturnsBackendUnavailable()
    {
        var native = new FakeX11NativeApi { OpenDisplayException = new DllNotFoundException("libX11") };
        var probe = new X11ScreenCaptureSupportProbe(native, _ => ":0");

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("libX11", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, native.CloseDisplayCalls);
    }

    private sealed class FakeX11NativeApi : IX11NativeApi
    {
        public IntPtr Display { get; } = new(1);

        public IntPtr Root { get; init; } = new(2);

        public uint Width { get; init; } = 100;

        public uint Height { get; init; } = 80;

        public Exception? OpenDisplayException { get; init; }

        public int GetGeometryCalls { get; private set; }

        public int CloseDisplayCalls { get; private set; }

        public IntPtr OpenDisplay(string? display)
        {
            if (OpenDisplayException is not null)
            {
                throw OpenDisplayException;
            }

            return Display;
        }

        public int CloseDisplay(IntPtr display)
        {
            Assert.Equal(Display, display);
            CloseDisplayCalls++;
            return 0;
        }

        public IntPtr DefaultRootWindow(IntPtr display)
        {
            Assert.Equal(Display, display);
            return Root;
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
            Assert.Equal(Display, display);
            Assert.Equal(Root, drawable);
            GetGeometryCalls++;
            root = Root;
            x = 0;
            y = 0;
            width = Width;
            height = Height;
            borderWidth = 0;
            depth = 24;
            return 1;
        }

        public IntPtr GetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, UIntPtr planeMask, int format) => throw new NotSupportedException();

        public UIntPtr GetPixel(IntPtr ximage, int x, int y) => throw new NotSupportedException();

        public int DestroyImage(IntPtr ximage) => throw new NotSupportedException();

        public XImage ReadImage(IntPtr ximage) => throw new NotSupportedException();
    }
}
