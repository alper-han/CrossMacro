
namespace CrossMacro.Platform.Windows.Services.ScreenReading;

public sealed class WindowsScreenFrameProvider : IScreenFrameProvider
{
    private readonly IWindowsScreenCaptureBackend _captureBackend;
    private readonly Func<bool> _isSupportedProbe;
    private bool _disposed;

    public WindowsScreenFrameProvider()
        : this(new GdiWindowsScreenCaptureBackend(), OperatingSystem.IsWindows) { /* Empty */ }

    internal WindowsScreenFrameProvider(
        IWindowsScreenCaptureBackend captureBackend,
        Func<bool> isSupportedProbe)
    {
        _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
        _isSupportedProbe = isSupportedProbe ?? throw new ArgumentNullException(nameof(isSupportedProbe));
    }

    public string ProviderName => "Windows GDI BitBlt";

    public bool IsSupported => _isSupportedProbe();

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsSupported)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Unsupported,
                "Windows GDI screen reading is available only on Windows desktop sessions.");
        }

        if (options.CancellationToken.IsCancellationRequested)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "Windows GDI screen capture was canceled before it started.");
        }

        try
        {
            var virtualScreen = _captureBackend.GetVirtualScreenBounds();
            var captureRegion = region ?? virtualScreen;
            if (!virtualScreen.Contains(captureRegion))
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Requested region {captureRegion} is outside Windows virtual screen bounds {virtualScreen}.");
            }

            var captured = _captureBackend.Capture(captureRegion, options.CancellationToken);
            ScreenFrame? frame = null;
            try
            {
                frame = new ScreenFrame(
                    captured.LogicalBounds,
                    captured.Stride,
                    captured.PixelFormat,
                    captured.Pixels);
                return ScreenReadResultFactory.Success(frame);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                frame?.Dispose();
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "Windows GDI screen capture was canceled.");
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or ExternalException or Win32Exception or InvalidOperationException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                ex.Message);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
