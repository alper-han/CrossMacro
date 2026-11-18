
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class MacOSScreenFrameProvider(IMacOSScreenCaptureBackend captureBackend, IMacOSPermissionNative permissions) : IScreenFrameProvider
{
    private const string PermissionMessage = "macOS Screen Recording permission is required for pixelcolor, waitcolor, and pixelsearch. Enable it in System Settings > Privacy & Security > Screen Recording, then restart CrossMacro.";

    private readonly IMacOSScreenCaptureBackend _captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
    private readonly IMacOSPermissionNative _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    private int _permissionRequested;
    private bool _disposed;

    public string ProviderName => "macOS CoreGraphics";

    public bool IsSupported => true;

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(CaptureFrame(region, options));
    }

    private ScreenReadResult<ScreenFrame> CaptureFrame(ScreenRect? region, ScreenReadOptions options)
    {
        if (options.CancellationToken.IsCancellationRequested)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled before it started.");
        }

        try
        {
            var virtualScreen = _captureBackend.GetVirtualScreenBounds();
            var captureRegion = region ?? virtualScreen;
            if (!virtualScreen.Contains(captureRegion))
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(
                    ScreenReadErrorKind.OutOfBounds,
                    $"Requested region {captureRegion} is outside macOS virtual screen bounds {virtualScreen}.");
            }

            if (!EnsurePermission())
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.PermissionDenied, PermissionMessage);
            }

            var captured = _captureBackend.Capture(captureRegion, options.CancellationToken);
            var frame = new ScreenFrame(
                captured.LogicalBounds,
                captured.Stride,
                captured.PixelFormat,
                captured.Pixels,
                validPixelMask: captured.ValidPixelMask ?? [],
                alphaMode: ScreenAlphaMode.Opaque);
            return ScreenReadResultFactory.Success(frame);
        }
        catch (OperationCanceledException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "macOS CoreGraphics screen capture was canceled.");
        }
        catch (BackendUnavailableException ex)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ArithmeticException or ExternalException or InvalidOperationException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.CaptureFailed, ex.Message);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Asks macOS for Screen Recording access once per provider; polling commands must not re-prompt.
    /// Opening System Settings is left to the permission gate the user interacts with.
    /// </summary>
    private bool EnsurePermission()
    {
        if (_permissions.PreflightScreenCapture())
        {
            return true;
        }

        if (Interlocked.Exchange(ref _permissionRequested, 1) is 0)
        {
            _ = _permissions.RequestScreenCapture();
        }

        return _permissions.PreflightScreenCapture();
    }
}
