using System.Numerics;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Native.X11;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public sealed class X11ScreenCapture : IX11ScreenCapture
{
    private const string ProviderDescription = "X11 XGetImage";
    private const int ZPixmap = 2;
    private static readonly UIntPtr AllPlanes = new(ulong.MaxValue);

    private readonly IX11ScreenCaptureSupportProbe _supportProbe;
    private readonly IX11NativeApi _native;
    private readonly Lock _lock = new();
    private bool _disposed;

    public X11ScreenCapture()
        : this(X11ScreenCaptureSupportProbe.Instance, X11NativeApi.Instance)
    {
    }

    internal X11ScreenCapture(IX11ScreenCaptureSupportProbe supportProbe, IX11NativeApi native)
    {
        _supportProbe = supportProbe ?? throw new ArgumentNullException(nameof(supportProbe));
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public X11ScreenCaptureSupportResult ProbeSupport() => _supportProbe.ProbeSupport();

    public Task<X11ScreenCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var support = ProbeSupport();
        return support.IsSupported
            ? CaptureSupportedAsync(region, options)
            : Task.FromResult(X11ScreenCaptureResult.Failure(
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                support.ErrorMessage ?? "X11 screen capture is unavailable."));
    }

    public Task<X11ScreenCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(X11ScreenCaptureResult.Failure(
                ScreenReadErrorKind.Canceled,
                "X11 screen capture was canceled before it started."));
        }

        try
        {
            return Task.FromResult(CaptureFrame(region, options));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(X11ScreenCaptureResult.Failure(ScreenReadErrorKind.Canceled, "X11 screen capture was canceled."));
        }
        catch (Exception ex) when (IsKnownCaptureException(ex))
        {
            return Task.FromResult(X11ScreenCaptureResult.Failure(MapErrorKind(ex), ex.Message));
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private X11ScreenCaptureResult CaptureFrame(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            options.CancellationToken.ThrowIfCancellationRequested();

            var display = OpenDisplay();
            try
            {
                var root = _native.DefaultRootWindow(display);
                var rootBounds = GetRootBounds(display, root);
                var captureRegion = requestedRegion ?? rootBounds;
                if (!rootBounds.Contains(captureRegion))
                {
                    return X11ScreenCaptureResult.Failure(
                        ScreenReadErrorKind.OutOfBounds,
                        $"Requested region {captureRegion} is outside X11 root window bounds {rootBounds}.");
                }

                options.CancellationToken.ThrowIfCancellationRequested();
                var image = _native.GetImage(
                    display,
                    root,
                    captureRegion.X,
                    captureRegion.Y,
                    checked((uint)captureRegion.Width),
                    checked((uint)captureRegion.Height),
                    AllPlanes,
                    ZPixmap);

                if (image == IntPtr.Zero)
                {
                    return X11ScreenCaptureResult.Failure(
                        ScreenReadErrorKind.CaptureFailed,
                        $"{ProviderDescription} returned no image for region {captureRegion}.");
                }

                try
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var frame = CopyImage(image, _native.ReadImage(image), captureRegion, options.CancellationToken);
                    options.CancellationToken.ThrowIfCancellationRequested();
                    return X11ScreenCaptureResult.Success(frame);
                }
                finally
                {
                    _native.DestroyImage(image);
                }
            }
            finally
            {
                _native.CloseDisplay(display);
            }
        }
    }

    private IntPtr OpenDisplay()
    {
        var display = _native.OpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open the X11 display for screen capture.");
        }

        return display;
    }

    private ScreenRect GetRootBounds(IntPtr display, IntPtr root)
    {
        var status = _native.GetGeometry(display, root, out _, out _, out _, out var width, out var height, out _, out _);
        if (status == 0 || width == 0 || height == 0)
        {
            throw new InvalidOperationException("Failed to read X11 root window geometry for screen capture.");
        }

        return new ScreenRect(0, 0, checked((int)width), checked((int)height));
    }

    private X11ScreenCaptureFrame CopyImage(
        IntPtr image,
        XImage ximage,
        ScreenRect logicalBounds,
        CancellationToken cancellationToken)
    {
        if (ximage.Width < logicalBounds.Width || ximage.Height < logicalBounds.Height)
        {
            throw new InvalidOperationException(
                $"X11 image dimensions {ximage.Width}x{ximage.Height} are smaller than requested region {logicalBounds.Width}x{logicalBounds.Height}.");
        }

        ValidateRgbMasks(ximage);
        var stride = checked(logicalBounds.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Xrgb8888));
        var pixels = new byte[checked(stride * logicalBounds.Height)];

        for (var y = 0; y < logicalBounds.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < logicalBounds.Width; x++)
            {
                var pixel = _native.GetPixel(image, x, y).ToUInt64();
                var offset = checked(y * stride + x * 4);
                pixels[offset] = ExtractChannel(pixel, ximage.BlueMask.ToUInt64());
                pixels[offset + 1] = ExtractChannel(pixel, ximage.GreenMask.ToUInt64());
                pixels[offset + 2] = ExtractChannel(pixel, ximage.RedMask.ToUInt64());
                pixels[offset + 3] = 0;
            }
        }

        return new X11ScreenCaptureFrame(logicalBounds, stride, ScreenPixelFormat.Xrgb8888, pixels);
    }

    private static void ValidateRgbMasks(XImage image)
    {
        if (image.RedMask == UIntPtr.Zero || image.GreenMask == UIntPtr.Zero || image.BlueMask == UIntPtr.Zero)
        {
            throw new InvalidOperationException("X11 image did not report RGB channel masks.");
        }
    }

    private static byte ExtractChannel(ulong pixel, ulong mask)
    {
        var bitCount = BitOperations.PopCount(mask);
        if (bitCount is <= 0 or > 16)
        {
            throw new InvalidOperationException($"Unsupported X11 RGB channel mask 0x{mask:X}.");
        }

        var raw = (pixel & mask) >> BitOperations.TrailingZeroCount(mask);
        if (bitCount == 8)
        {
            return checked((byte)raw);
        }

        var max = (1UL << bitCount) - 1;
        return checked((byte)((raw * byte.MaxValue + max / 2) / max));
    }

    private static bool IsKnownCaptureException(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or OverflowException or UnauthorizedAccessException;

    private static ScreenReadErrorKind MapErrorKind(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException
            ? ScreenReadErrorKind.BackendUnavailable
            : ScreenReadErrorKind.CaptureFailed;
}

public sealed class X11ScreenCaptureSupportProbe : IX11ScreenCaptureSupportProbe
{
    private const string DisplayEnvironmentVariable = "DISPLAY";

    public static X11ScreenCaptureSupportProbe Instance { get; } = new(X11NativeApi.Instance, Environment.GetEnvironmentVariable);

    private readonly IX11NativeApi _native;
    private readonly Func<string, string?> _getEnvironmentVariable;

    internal X11ScreenCaptureSupportProbe(IX11NativeApi native, Func<string, string?> getEnvironmentVariable)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    public X11ScreenCaptureSupportResult ProbeSupport()
    {
        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(DisplayEnvironmentVariable)))
        {
            return X11ScreenCaptureSupportResult.Unsupported("DISPLAY is not set; X11 screen reading requires a native X11 session.");
        }

        try
        {
            var display = _native.OpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                return X11ScreenCaptureSupportResult.Unsupported("Failed to open the X11 display for screen reading.");
            }

            try
            {
                var root = _native.DefaultRootWindow(display);
                var status = _native.GetGeometry(display, root, out _, out _, out _, out var width, out var height, out _, out _);
                return status != 0 && width > 0 && height > 0
                    ? X11ScreenCaptureSupportResult.Supported()
                    : X11ScreenCaptureSupportResult.Unsupported("Failed to read X11 root window geometry for screen reading.");
            }
            finally
            {
                _native.CloseDisplay(display);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return X11ScreenCaptureSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }
}
