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
