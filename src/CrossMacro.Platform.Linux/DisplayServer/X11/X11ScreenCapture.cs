using System.Numerics;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public sealed class X11ScreenCapture : IX11ScreenCapture
{
    private const string ProviderDescription = "X11 XGetImage";
    private const int ByteOrderMsbFirst = 1;

    private readonly IX11ScreenCaptureSupportProbe _supportProbe;
    private readonly IX11NativeApi _native;
    private readonly Lock _lock = new();
    private bool _disposed;

    public X11ScreenCapture()
        : this(X11ScreenCaptureSupportProbe.Instance, X11NativeApi.Instance) { /* Empty */ }

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
                    X11Native.AllPlanes,
                    X11Native.ZPixmap);

                if (image == IntPtr.Zero)
                {
                    return X11ScreenCaptureResult.Failure(
                        ScreenReadErrorKind.CaptureFailed,
                        $"{ProviderDescription} returned no image for region {captureRegion}.");
                }

                try
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var frame = CopyImage(_native.ReadImage(image), captureRegion, options.CancellationToken);
                    options.CancellationToken.ThrowIfCancellationRequested();
                    return X11ScreenCaptureResult.Success(frame);
                }
                finally
                {
                    _ = _native.DestroyImage(image);
                }
            }
            finally
            {
                _ = _native.CloseDisplay(display);
            }
        }
    }

    private IntPtr OpenDisplay()
    {
        var display = _native.OpenDisplay(display: null);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open the X11 display for screen capture.");
        }

        return display;
    }

    private ScreenRect GetRootBounds(IntPtr display, IntPtr root)
    {
        var status = _native.GetGeometry(display, root, out _, out _, out _, out var width, out var height, out _, out _);
        if (status is 0 || width == 0 || height == 0)
        {
            throw new InvalidOperationException("Failed to read X11 root window geometry for screen capture.");
        }

        return new ScreenRect(0, 0, checked((int)width), checked((int)height));
    }

    private static X11ScreenCaptureFrame CopyImage(XImage image, ScreenRect logicalBounds, CancellationToken cancellationToken)
    {
        if (image.Width < logicalBounds.Width || image.Height < logicalBounds.Height)
        {
            throw new InvalidOperationException(
                $"X11 image dimensions {image.Width.ToString(CultureInfo.InvariantCulture)}x{image.Height.ToString(CultureInfo.InvariantCulture)} are smaller than requested region {logicalBounds.Width.ToString(CultureInfo.InvariantCulture)}x{logicalBounds.Height.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (image.RedMask == UIntPtr.Zero || image.GreenMask == UIntPtr.Zero || image.BlueMask == UIntPtr.Zero)
        {
            throw new InvalidOperationException("X11 image did not report RGB channel masks.");
        }

        var bytesPerPixel = image.BitsPerPixel / 8;
        if (image.BitsPerPixel % 8 is not 0 || bytesPerPixel is < 2 or > 4)
        {
            throw new InvalidOperationException(
                $"Unsupported X11 image depth of {image.BitsPerPixel.ToString(CultureInfo.InvariantCulture)} bits per pixel.");
        }

        if (image.Data == IntPtr.Zero || image.BytesPerLine < checked(logicalBounds.Width * bytesPerPixel))
        {
            throw new InvalidOperationException("X11 image reported no pixel data or a row stride smaller than one row.");
        }

        var red = RgbChannel.Create(image.RedMask.ToUInt64());
        var green = RgbChannel.Create(image.GreenMask.ToUInt64());
        var blue = RgbChannel.Create(image.BlueMask.ToUInt64());
        var msbFirst = image.ByteOrder is ByteOrderMsbFirst;

        var source = new byte[checked(image.BytesPerLine * logicalBounds.Height)];
        Marshal.Copy(image.Data, source, 0, source.Length);

        var stride = checked(logicalBounds.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Xrgb8888));
        var pixels = new byte[checked(stride * logicalBounds.Height)];
        for (var y = 0; y < logicalBounds.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceRow = source.AsSpan(y * image.BytesPerLine, logicalBounds.Width * bytesPerPixel);
            var destinationRow = pixels.AsSpan(y * stride, stride);
            for (var x = 0; x < logicalBounds.Width; x++)
            {
                var pixel = ReadPixel(sourceRow.Slice(x * bytesPerPixel, bytesPerPixel), msbFirst);
                var offset = x * 4;
                destinationRow[offset] = blue.Extract(pixel);
                destinationRow[offset + 1] = green.Extract(pixel);
                destinationRow[offset + 2] = red.Extract(pixel);
                destinationRow[offset + 3] = 0;
            }
        }

        return new X11ScreenCaptureFrame(logicalBounds, stride, ScreenPixelFormat.Xrgb8888, pixels);
    }

    private static ulong ReadPixel(ReadOnlySpan<byte> bytes, bool msbFirst)
    {
        ulong pixel = 0;
        if (msbFirst)
        {
            foreach (var value in bytes)
            {
                pixel = (pixel << 8) | value;
            }
        }
        else
        {
            for (var i = bytes.Length - 1; i >= 0; i--)
            {
                pixel = (pixel << 8) | bytes[i];
            }
        }

        return pixel;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct RgbChannel(ulong Mask, int Shift, ulong Max)
    {
        public static RgbChannel Create(ulong mask)
        {
            var bitCount = BitOperations.PopCount(mask);
            if (bitCount is <= 0 or > 16)
            {
                throw new InvalidOperationException($"Unsupported X11 RGB channel mask 0x{mask.ToString("X", CultureInfo.InvariantCulture)}.");
            }

            return new RgbChannel(mask, BitOperations.TrailingZeroCount(mask), (1UL << bitCount) - 1);
        }

        public byte Extract(ulong pixel)
        {
            var raw = (pixel & Mask) >> Shift;
            return Max is byte.MaxValue
                ? checked((byte)raw)
                : checked((byte)(((raw * byte.MaxValue) + (Max / 2)) / Max));
        }
    }

    private static bool IsKnownCaptureException(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or OverflowException or UnauthorizedAccessException;

    private static ScreenReadErrorKind MapErrorKind(Exception exception) =>
        exception is DllNotFoundException or EntryPointNotFoundException
            ? ScreenReadErrorKind.BackendUnavailable
            : ScreenReadErrorKind.CaptureFailed;
}
