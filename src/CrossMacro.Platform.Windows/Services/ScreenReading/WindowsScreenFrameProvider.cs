using System.ComponentModel;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services.ScreenReading;

public sealed class WindowsScreenFrameProvider : IScreenFrameProvider
{
    private readonly IWindowsScreenCaptureBackend _captureBackend;
    private readonly Func<bool> _isSupportedProbe;
    private bool _disposed;

    public WindowsScreenFrameProvider()
        : this(new GdiWindowsScreenCaptureBackend(), OperatingSystem.IsWindows)
    {
    }

    internal WindowsScreenFrameProvider(
        IWindowsScreenCaptureBackend captureBackend,
        Func<bool> isSupportedProbe)
    {
        _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
        _isSupportedProbe = isSupportedProbe ?? throw new ArgumentNullException(nameof(isSupportedProbe));
    }

    public string ProviderName => "Windows GDI BitBlt";

    public bool IsSupported => _isSupportedProbe();

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsSupported)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Unsupported,
                "Windows GDI screen reading is available only on Windows desktop sessions."));
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Canceled,
                "Windows GDI screen capture was canceled before it started."));
        }

        try
        {
            var virtualScreen = _captureBackend.GetVirtualScreenBounds();
            var captureRegion = region ?? virtualScreen;
            if (!virtualScreen.Contains(captureRegion))
            {
                return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Requested region {captureRegion} is outside Windows virtual screen bounds {virtualScreen}."));
            }

            var captured = _captureBackend.Capture(captureRegion, options.CancellationToken);
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(new ScreenFrame(
                captured.LogicalBounds,
                captured.Stride,
                captured.PixelFormat,
                captured.Pixels)));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Canceled,
                "Windows GDI screen capture was canceled."));
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or ExternalException or Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.CaptureFailed,
                ex.Message));
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

internal interface IWindowsScreenCaptureBackend
{
    ScreenRect GetVirtualScreenBounds();

    WindowsScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}

internal sealed record WindowsScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels);

internal sealed class GdiWindowsScreenCaptureBackend : IWindowsScreenCaptureBackend
{
    private const ushort BitsPerPixel = 32;

    public ScreenRect GetVirtualScreenBounds()
    {
        var x = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
        var y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
        var width = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN);
        var height = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Windows virtual screen dimensions are invalid: {width}x{height}.");
        }

        return new ScreenRect(x, y, width, height);
    }

    public WindowsScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var screenDc = User32.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw CreateWin32Exception("GetDC(NULL) failed");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousObject = IntPtr.Zero;
        try
        {
            memoryDc = Gdi32.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw CreateWin32Exception("CreateCompatibleDC failed");
            }

            var bitmapInfo = CreateBitmapInfo(region.Width, region.Height);
            bitmap = Gdi32.CreateDIBSection(
                screenDc,
                ref bitmapInfo,
                Gdi32.DibRgbColors,
                out var bits,
                IntPtr.Zero,
                0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
            {
                throw CreateWin32Exception("CreateDIBSection failed");
            }

            previousObject = Gdi32.SelectObject(memoryDc, bitmap);
            if (previousObject == IntPtr.Zero || previousObject == Gdi32.HbitmapError)
            {
                throw CreateWin32Exception("SelectObject failed");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!Gdi32.BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.X,
                    region.Y,
                    Gdi32.Srccopy | Gdi32.CaptureBlt))
            {
                throw CreateWin32Exception("BitBlt failed");
            }

            if (!Gdi32.GdiFlush())
            {
                throw CreateWin32Exception("GdiFlush failed");
            }

            var stride = checked(region.Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888));
            var pixels = new byte[checked(stride * region.Height)];
            Marshal.Copy(bits, pixels, 0, pixels.Length);

            return new WindowsScreenCaptureFrame(region, stride, ScreenPixelFormat.Bgra8888, pixels);
        }
        finally
        {
            if (previousObject != IntPtr.Zero && previousObject != Gdi32.HbitmapError && memoryDc != IntPtr.Zero)
            {
                Gdi32.SelectObject(memoryDc, previousObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                Gdi32.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                Gdi32.DeleteDC(memoryDc);
            }

            User32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static BITMAPINFO CreateBitmapInfo(int width, int height)
    {
        return new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = checked(-height),
                biPlanes = 1,
                biBitCount = BitsPerPixel,
                biCompression = Gdi32.BiRgb,
                biSizeImage = (uint)checked(width * height * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Bgra8888))
            }
        };
    }

    private static Win32Exception CreateWin32Exception(string operation)
    {
        var error = Marshal.GetLastPInvokeError();
        return error == 0
            ? new Win32Exception($"{operation}.")
            : new Win32Exception(error, $"{operation}: {new Win32Exception(error).Message}");
    }
}
